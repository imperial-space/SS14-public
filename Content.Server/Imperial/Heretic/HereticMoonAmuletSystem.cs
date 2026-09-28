using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
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
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

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
        SetMoonBladeStats(args.Wearer, equipped: true);
    }

    private void OnUnequipped(Entity<HereticMoonAmuletComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        RemComp<ThermalEntityVisionComponent>(args.Wearer);
        RemComp<HereticMoonAmuletEquippedComponent>(args.Wearer);
        SetMoonBladeStats(args.Wearer, equipped: false);
    }

    private void SetMoonBladeStats(EntityUid wearer, bool equipped)
    {
        if (!_inventory.TryGetSlotEntity(wearer, "belt", out var belt) || belt == null)
            return;
        if (!HasComp<HereticMoonBladeComponent>(belt.Value))
            return;
        if (!TryComp<MeleeWeaponComponent>(belt.Value, out var melee))
            return;
        if (melee.Damage.DamageDict.ContainsKey("Slash"))
            melee.Damage.DamageDict["Slash"] = equipped ? FixedPoint2.Zero : FixedPoint2.New(18);
        melee.ResistanceBypass = equipped;
        Dirty(belt.Value, melee);
    }

    private void OnMeleeHit(Entity<HereticMoonAmuletComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit) return;
        if (!TryComp<HereticComponent>(args.User, out var heretic)) return;
        if (heretic.CurrentPath != HereticPath.Moon) return;

        foreach (var target in args.HitEntities)
        {
            if (!HasComp<MobStateComponent>(target)) continue;
            if (HasComp<HereticComponent>(target)) continue;

            var brainComp = EnsureComp<HereticMoonBrainDamageComponent>(target);

            // SS13: sanity >= INSANE (10) → drain 20 sanity, no conversion yet
            if (brainComp.Sanity >= 10f)
            {
                brainComp.Sanity = Math.Max(0f, brainComp.Sanity - 20f);
                continue;
            }

            // SS13: sanity < INSANE + MindShield → 2-мин сон
            if (HasComp<MindShieldComponent>(target))
            {
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(120), true);
                _popup.PopupEntity(Loc.GetString("heretic-moon-amulet-slept"), target, target, PopupType.LargeCaution);
                continue;
            }

            // SS13: sanity < INSANE + no shield → moon_converted (берсерк)
            TryMoonConvert(target, brainComp.Sanity);
        }
    }

    /// <summary>
    /// Применяет moon_converted: хил, knockdown, mute, overlay до 75 урона.
    /// Вызывается как из удара амулета, так и из ауры возвышения.
    /// </summary>
    public void TryMoonConvert(EntityUid target, float sanity)
    {
        if (HasComp<HereticMoonConvertedComponent>(target)) return;

        // SS13: heal_overall_damage(brute = 150-sanity, fire = 150-sanity)
        var healAmount = FixedPoint2.New((int)(150f - sanity));
        var heal = new DamageSpecifier();
        heal.DamageDict["Blunt"]    = -healAmount;
        heal.DamageDict["Heat"]     = -healAmount;
        _damage.TryChangeDamage(target, heal, ignoreResistances: true);

        // SS13: Stun 60 сек + немота 1ч
        _stun.TryKnockdown(target, TimeSpan.FromSeconds(60), true);
        _statusEffects.TryAddStatusEffect<MutedComponent>(target, "Muted", TimeSpan.FromHours(1), true);

        // SS13: moon_converted — последователь еретика, отображается оверлеем
        EnsureComp<HereticMoonConvertedComponent>(target);

        // SS13: to_chat "ЛУНА УКАЗЫВАЕТ..." (только цели) + balloon_alert "они лгут..." (видно окружающим)
        _popup.PopupEntity(Loc.GetString("heretic-moon-converted-message"), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("heretic-moon-converted-balloon"), target, PopupType.MediumCaution);
        // SS13: cause_hallucination(owner, affects_others = TRUE)
        _hereticEffects.ApplyHallucination(target, TimeSpan.FromSeconds(30));
    }

    private void OnConvertedDamage(EntityUid uid, HereticMoonConvertedComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null) return;

        foreach (var val in args.DamageDelta.DamageDict.Values)
        {
            if (val > 0) comp.DamageAccumulated += val.Float();
        }

        if (comp.DamageAccumulated < HereticMoonConvertedComponent.DamageBreakThreshold) return;

        // SS13: при 75+ урона moon_converted снимается
        _popup.PopupEntity(Loc.GetString("heretic-moon-converted-cleansed"), uid, uid, PopupType.Large);
        RemComp<HereticMoonConvertedComponent>(uid);
        _statusEffects.TryRemoveStatusEffect(uid, "Muted");
        _stun.TryKnockdown(uid, TimeSpan.FromSeconds(5), true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<HereticComponent, HereticMoonBrainDamageComponent>();
        while (query.MoveNext(out var uid, out var heretic, out var brain))
        {
            if (heretic.CurrentPath != HereticPath.Moon) continue;
            if (brain.BrainDamage <= 0f) continue;
            if (heretic.PassiveLevel < 1) continue;

            float baseRate = heretic.PassiveLevel switch
            {
                1 => 1f,
                2 => 2f,
                _ => 3f
            };

            if (HasComp<HereticMoonAmuletEquippedComponent>(uid))
                baseRate *= 2f;

            if (TryComp<HereticMoonPassiveComponent>(uid, out var passive) && passive.IsInCombat(curTime))
                baseRate *= 0.5f;

            brain.BrainDamage = Math.Max(0f, brain.BrainDamage - baseRate * frameTime);
        }
    }

    public void ApplyChannelAmulet(EntityUid caster, EntityUid target, HereticMoonBrainDamageComponent brainComp)
    {
        if (brainComp.Sanity >= 10f)
        {
            brainComp.Sanity = Math.Max(0f, brainComp.Sanity - 20f);
            return;
        }

        if (HasComp<MindShieldComponent>(target))
        {
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(120), true);
            _popup.PopupEntity(Loc.GetString("heretic-moon-amulet-slept"), target, target, PopupType.LargeCaution);
            return;
        }

        TryMoonConvert(target, brainComp.Sanity);
    }
}
