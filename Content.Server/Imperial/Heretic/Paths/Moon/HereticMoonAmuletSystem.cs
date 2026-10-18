using Content.Server.Imperial.Heretic;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic.Items;
using Content.Server.Imperial.Heretic.Paths.Moon;
using Content.Server.Imperial.Heretic.Paths.Moon.Parade;
using Content.Server.Imperial.Heretic.Paths.Void;
using Content.Shared.Alert;
using Content.Shared.Clothing;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Imperial.Lavaland.ColossusLoot;
using Content.Shared.Inventory;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic.Paths.Moon;

public sealed class HereticMoonAmuletSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem          _damage        = default!;
    [Dependency] private readonly InventorySystem           _inventory     = default!;
    [Dependency] private readonly StatusEffectsSystem       _statusEffects = default!;
    [Dependency] private readonly SharedStunSystem          _stun          = default!;
    [Dependency] private readonly HereticMoonBrainDamageSystem _brainDamage = default!;
    [Dependency] private readonly HereticStatusEffectsSystem   _hereticEffects = default!;
    [Dependency] private readonly MobStateSystem            _mobs          = default!;
    [Dependency] private readonly SharedPopupSystem         _popup         = default!;
    [Dependency] private readonly IGameTiming               _gameTiming    = default!;
    [Dependency] private readonly Content.Shared.StatusEffectNew.StatusEffectsSystem _newStatusEffects = default!;

    /// <summary>SANITY_LEVEL_INSANE: ниже этого рассудок считается сломанным.</summary>
    private const float InsaneSanity = 25f;

    /// <summary>sanity_damage амулета.</summary>
    private const float SanityDamage = 20f;

    /// <summary>Не еретику, надевшему амулет, сразу выжигает половину рассудка.</summary>
    private const float NonHereticSanityLoss = 50f;

    /// <summary>adjust_fire_loss(30) для нечеловеческой цели.</summary>
    private const float NonHumanBurn = 30f;

    /// <summary>adjust_organ_loss(ORGAN_SLOT_BRAIN, 25) по уже обработанной цели.</summary>
    private const float AlreadyTouchedBrainDamage = 25f;

    /// <summary>status_effect/moon_slept: duration = 2 MINUTES.</summary>
    private static readonly TimeSpan MoonSleptDuration = TimeSpan.FromMinutes(2);

    private static readonly EntProtoId ForcedSleepingId = "StatusEffectForcedSleeping";

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";
    private static readonly ProtoId<AlertPrototype> ConvertedAlert = "HereticMoonConverted";

    /// <summary>moon_converted: SetUnconscious(60 SECONDS).</summary>
    private static readonly TimeSpan ConvertedUnconscious = TimeSpan.FromSeconds(60);

    /// <summary>moon_converted/on_remove(): AdjustUnconscious(5 SECONDS).</summary>
    private static readonly TimeSpan CuredUnconscious = TimeSpan.FromSeconds(5);

    [Dependency] private readonly AlertsSystem _alerts = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMoonAmuletComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HereticMoonAmuletComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<HereticMoonAmuletComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<HereticMoonConvertedComponent, DamageChangedEvent>(OnConvertedDamage);
    }

    private void OnEquipped(Entity<HereticMoonAmuletComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (!HasComp<HereticComponent>(args.Wearer))
        {
            var brainComp = EnsureComp<HereticMoonBrainDamageComponent>(args.Wearer);
            brainComp.Sanity = Math.Max(0f, brainComp.Sanity - 50f);
            _popup.PopupEntity(Loc.GetString("heretic-moon-amulet-nonheretic"), args.Wearer, args.Wearer, PopupType.LargeCaution);
            return;
        }

        EnsureComp<ThermalEntityVisionComponent>(args.Wearer);
        EnsureComp<HereticMoonAmuletEquippedComponent>(args.Wearer);
    }

    private void OnUnequipped(Entity<HereticMoonAmuletComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        RemComp<ThermalEntityVisionComponent>(args.Wearer);
        RemComp<HereticMoonAmuletEquippedComponent>(args.Wearer);
    }

    /// <summary>attack(): амулетом бьют в руке, эффект тот же, что и у проведения.</summary>
    private void OnMeleeHit(Entity<HereticMoonAmuletComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit) return;

        foreach (var target in args.HitEntities)
        {
            ChannelAmulet(args.User, target);
        }
    }

    /// <summary>
    /// Применяет moon_converted: хил, knockdown, mute, overlay до 75 урона.
    /// Вызывается как из удара амулета, так и из ауры возвышения.
    /// </summary>
    public void TryMoonConvert(EntityUid target, float sanity)
    {
        if (HasComp<HereticMoonConvertedComponent>(target)) return;

        // on_apply(): лечит, чтобы даже крит мог послужить еретику.
        // adjust_brute_loss(-150 + sanity) + adjust_fire_loss(-150 + sanity)
        var healAmount = -FixedPoint2.New(Math.Max(0f, 150f - sanity));
        _damage.HealDistributed(target, healAmount, BruteGroup);
        _damage.HealDistributed(target, healAmount, BurnGroup);

        // SetUnconscious(60 SECONDS) + TRAIT_MUTE на всё время эффекта.
        _newStatusEffects.TryAddStatusEffectDuration(target, ForcedSleepingId, ConvertedUnconscious);
        _statusEffects.TryAddStatusEffect<MutedComponent>(target, "Muted", TimeSpan.FromHours(1), true);

        // moon_converted: оверлей безумия рисует клиент по этому компоненту.
        EnsureComp<HereticMoonConvertedComponent>(target);
        _alerts.ShowAlert(target, ConvertedAlert);

        _popup.PopupEntity(Loc.GetString("heretic-moon-converted-message"), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("heretic-moon-converted-balloon"), target, PopupType.MediumCaution);

        // cause_hallucination(delusion/preset/moon, affects_us = FALSE, affects_others = TRUE):
        // берсерк видит всех вокруг лунными массами, пока эффект не снят.
        _hereticEffects.ApplyMoonDelusion(target, TimeSpan.FromHours(1));
    }

    /// <summary>on_damaged(): 75 урона (кроме выносливости) возвращают рассудок.</summary>
    private void OnConvertedDamage(EntityUid uid, HereticMoonConvertedComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null) return;

        foreach (var val in args.DamageDelta.DamageDict.Values)
        {
            if (val > 0) comp.DamageAccumulated += val.Float();
        }

        if (comp.DamageAccumulated < HereticMoonConvertedComponent.DamageBreakThreshold) return;

        CureMoonConvert(uid);
    }

    /// <summary>on_remove(): разум очищен, но цель ещё 5 секунд без сознания.</summary>
    public void CureMoonConvert(EntityUid uid)
    {
        if (!RemComp<HereticMoonConvertedComponent>(uid))
            return;

        _popup.PopupEntity(Loc.GetString("heretic-moon-converted-cleansed"), uid, uid, PopupType.Large);
        _statusEffects.TryRemoveStatusEffect(uid, "Muted");
        _alerts.ClearAlert(uid, ConvertedAlert);
        _hereticEffects.RemoveMoonDelusion(uid);
        _newStatusEffects.TryAddStatusEffectDuration(uid, ForcedSleepingId, CuredUnconscious);
    }

    /// <summary>
    /// channel_amulet(): лестница проверок лунного амулета. Срабатывает от удара
    /// амулетом, от удара лунным клинком в амулете, от Врат Разума и от клонов Восшествия.
    /// </summary>
    public bool ChannelAmulet(EntityUid user, EntityUid target)
    {
        // Не еретик, надевший амулет, просто теряет рассудок.
        if (!HasComp<HereticComponent>(user))
        {
            var userBrain = EnsureComp<HereticMoonBrainDamageComponent>(user);
            userBrain.Sanity = Math.Max(0f, userBrain.Sanity - NonHereticSanityLoss);
            _popup.PopupEntity(Loc.GetString("heretic-moon-amulet-nonheretic"), user, user, PopupType.LargeCaution);
            return false;
        }

        if (target == user || !HasComp<MobStateComponent>(target))
            return false;

        // Нечеловеческая цель просто горит.
        if (!HasComp<HumanoidProfileComponent>(target))
        {
            var burn = new DamageSpecifier();
            burn.DamageDict["Heat"] = FixedPoint2.New(NonHumanBurn);
            _damage.TryChangeDamage(target, burn, ignoreResistances: false);
            return true;
        }

        if (HasComp<HereticComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("heretic-moon-amulet-resists"), target, user, PopupType.SmallCaution);
            return false;
        }

        // Уже усыплённым и обращённым амулет просто ломает мозг.
        if (HasComp<HereticMoonConvertedComponent>(target) || HasComp<HereticMoonSleptComponent>(target))
        {
            _brainDamage.AddBrainDamage(target, AlreadyTouchedBrainDamage);
            _popup.PopupEntity(Loc.GetString("heretic-moon-amulet-damage"), target, user, PopupType.MediumCaution);
            return false;
        }

        var brainComp = EnsureComp<HereticMoonBrainDamageComponent>(target);

        // Пока рассудок не на нуле — амулет только выедает его.
        if (brainComp.Sanity >= InsaneSanity)
        {
            brainComp.Sanity = Math.Max(0f, brainComp.Sanity - SanityDamage);
            _popup.PopupEntity(Loc.GetString("heretic-moon-amulet-too-strong"), target, user, PopupType.SmallCaution);
            return false;
        }

        // Защита разума не ломается, но и не выдерживает: цель засыпает.
        if (HasComp<MindShieldComponent>(target))
        {
            ApplyMoonSleep(target);
            return true;
        }

        TryMoonConvert(target, brainComp.Sanity);
        return true;
    }

    /// <summary>status_effect/moon_slept: сон на половину двухминутного эффекта.</summary>
    public void ApplyMoonSleep(EntityUid target)
    {
        EnsureComp<HereticMoonSleptComponent>(target).ExpiresAt = _gameTiming.CurTime + MoonSleptDuration;
        _newStatusEffects.TryAddStatusEffectDuration(target, ForcedSleepingId, MoonSleptDuration * 0.5);
        _popup.PopupEntity(Loc.GetString("heretic-moon-amulet-slept"), target, target, PopupType.LargeCaution);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<HereticMoonSleptComponent>();
        while (query.MoveNext(out var uid, out var slept))
        {
            if (now >= slept.ExpiresAt)
                RemComp<HereticMoonSleptComponent>(uid);
        }
    }
}
