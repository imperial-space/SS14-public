using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Electrocution;
using Content.Server.Flash;
using Content.Server.Lightning;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Magic.TurnedToStone;
using Content.Shared.Imperial.Antimagic;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.Spells;

#region Компоненты

/// <summary>smite: разорвать цель касанием, ослепив всех вокруг вспышкой.</summary>
[RegisterComponent]
public sealed partial class ImperialSmiteSpellComponent : Component;

/// <summary>flesh_to_stone: оглушение на 4 секунды и статуя на 8 минут.</summary>
[RegisterComponent]
public sealed partial class ImperialFleshToStoneSpellComponent : Component
{
    [DataField]
    public TimeSpan StatueTime = TimeSpan.FromMinutes(8);
}

/// <summary>scream_for_me: тяжёлые рваные раны по всему телу.</summary>
[RegisterComponent]
public sealed partial class ImperialScreamForMeSpellComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new() { DamageDict = { ["Slash"] = 60 } };

    [DataField]
    public float Bleed = 15f;
}

/// <summary>blind: ослепление на 20 секунд.</summary>
[RegisterComponent]
public sealed partial class ImperialBlindSpellComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);
}

/// <summary>barnyardcurse: проклятая маска животного прирастает к лицу.</summary>
[RegisterComponent]
public sealed partial class ImperialBarnyardSpellComponent : Component
{
    /// <summary>GLOB.cursed_animal_masks.</summary>
    [DataField]
    public List<EntProtoId> Masks = new();
}

/// <summary>untie_shoes: развязать, а затем связать шнурки цели.</summary>
[RegisterComponent]
public sealed partial class ImperialTieShoesSpellComponent : Component
{
    [DataField]
    public EntProtoId SummonedShoes = "ClothingShoesColorBlack";
}

/// <summary>Шнурки обуви: развязаны (спотыкаешься) или связаны узлом (падаешь).</summary>
[RegisterComponent]
public sealed partial class ImperialShoeLacesComponent : Component
{
    [ViewVariables]
    public ImperialLaceState State = ImperialLaceState.Tied;

    [ViewVariables]
    public TimeSpan NextTrip;
}

[Serializable, NetSerializable]
public enum ImperialLaceState : byte
{
    Tied,
    Untied,
    Knotted,
}

/// <summary>apply_mutations/mutate: халк, лазерное зрение и гигантизм на 30 секунд.</summary>
[RegisterComponent]
public sealed partial class ImperialMutateSpellComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(30);

    [DataField]
    public EntProtoId LaserAction = "ActionImperialLaserEyes";
}

/// <summary>rod_form: маг превращается в неподвижный стержень.</summary>
[RegisterComponent]
public sealed partial class ImperialRodFormSpellComponent : Component
{
    [DataField]
    public EntProtoId Rod = "ImperialWizardRod";

    /// <summary>BASE_WIZ_ROD_RANGE и distance_per_spell_rank.</summary>
    [DataField]
    public float BaseDistance = 13f;

    [DataField]
    public float DistancePerRank = 3f;

    [DataField]
    public float DamagePerRank = 20f;
}

/// <summary>Стержень с магом внутри.</summary>
[RegisterComponent]
public sealed partial class ImperialWizardRodComponent : Component
{
    [ViewVariables]
    public EntityUid? Wizard;

    [ViewVariables]
    public Vector2 Start;

    [ViewVariables]
    public float MaxDistance = 13f;

    /// <summary>70 + damage_bonus грубого урона пробитым.</summary>
    [ViewVariables]
    public float Damage = 70f;

    [ViewVariables]
    public HashSet<EntityUid> Hit = new();
}

/// <summary>/datum/action/cooldown/spell/conjure: призыв существ или предметов вокруг.</summary>
[RegisterComponent]
public sealed partial class ImperialConjureSpellComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Summons = new();

    [DataField]
    public int Amount = 1;

    /// <summary>Прибавка к количеству за уровень (summon_amount++ у обезьян).</summary>
    [DataField]
    public int AmountPerLevel;

    [DataField]
    public float Radius = 7f;

    /// <summary>summon_lifespan (0 — навсегда).</summary>
    [DataField]
    public TimeSpan Lifespan = TimeSpan.Zero;

    [DataField]
    public bool RespectsDensity;

    /// <summary>Фракция призванных; заклинатель на время становится её союзником.</summary>
    [DataField]
    public string? Faction;

    [DataField]
    public TimeSpan AllyTime = TimeSpan.FromMinutes(1);
}

/// <summary>charged/beam/tesla: накопить разряд и пустить по ближайшим целям.</summary>
[RegisterComponent]
public sealed partial class ImperialTeslaSpellComponent : Component
{
    [DataField]
    public TimeSpan ChannelTime = TimeSpan.FromSeconds(10);

    [DataField]
    public float Radius = 7f;

    [DataField]
    public int Bounces = 5;

    [DataField]
    public int Energy = 30;

    [DataField]
    public int EnergyLostPerBounce = 5;

    [ViewVariables]
    public bool Channeling;
}

/// <summary>Снаряд-молния: при попадании бьёт цепной молнией.</summary>
[RegisterComponent]
public sealed partial class ImperialLightningProjectileComponent : Component
{
    [DataField]
    public float Range = 15f;

    [DataField]
    public int Bolts = 3;
}

/// <summary>conjure_item: призвать предмет в руку.</summary>
[RegisterComponent]
public sealed partial class ImperialConjureItemSpellComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Item;

    /// <summary>delete_old: прежний призванный предмет исчезает.</summary>
    [DataField]
    public bool DeleteOld = true;

    [DataField]
    public bool RequiresHands;

    [ViewVariables]
    public List<EntityUid> Items = new();
}

/// <summary>Зачарованная винтовка: после выстрела отбрасывается, в руке появляется новая.</summary>
[RegisterComponent]
public sealed partial class ImperialInfiniteGunComponent : Component
{
    [DataField]
    public int GunsLeft = 30;

    /// <summary>Аркановый залп: просто исчезает, когда заряды кончатся.</summary>
    [DataField]
    public bool Barrage;
}

/// <summary>splattercasting: откаты в 10 раз короче, но каждое заклинание стоит крови.</summary>
[RegisterComponent]
public sealed partial class ImperialSplattercastingSpellComponent : Component;

/// <summary>Маг-вампир (/datum/component/splattercasting).</summary>
[RegisterComponent]
public sealed partial class ImperialSplattercasterComponent : Component
{
    /// <summary>Кровь уходит сама по себе.</summary>
    [ViewVariables]
    public TimeSpan NextDrain;
}

/// <summary>sanguine_strike: следующий удар оружием сильнее и пьёт кровь.</summary>
[RegisterComponent]
public sealed partial class ImperialSanguineStrikeSpellComponent : Component;

/// <summary>Оружие, зачарованное sanguine_strike.</summary>
[RegisterComponent]
public sealed partial class ImperialSanguineEnchantedComponent : Component
{
    [ViewVariables]
    public EntityUid Spell;
}

[Serializable, NetSerializable]
public sealed partial class ImperialSpellChannelDoAfterEvent : SimpleDoAfterEvent;

#endregion

public sealed partial class ImperialWizardSpellsSystem
{
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly FlashSystem _flash = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly Content.Shared.Body.Systems.SharedBloodstreamSystem _blood = default!;

    private const string RodContainer = "imperial_wizard_rod";
    private static readonly SoundSpecifier ShockSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/lightningshock.ogg");
    private static readonly SoundSpecifier ChargeUpSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/lightning_chargeup.ogg");

    /// <summary>Временные союзники призванных (фракция и когда её снять).</summary>
    private readonly List<(EntityUid Mob, string Faction, TimeSpan End)> _temporaryAllies = new();

    private void InitializeOffensive()
    {
        SubscribeLocalEvent<ImperialSmiteSpellComponent, ImperialTouchHitEvent>(OnSmite);
        SubscribeLocalEvent<ImperialFleshToStoneSpellComponent, ImperialTouchHitEvent>(OnFleshToStone);
        SubscribeLocalEvent<ImperialScreamForMeSpellComponent, ImperialTouchHitEvent>(OnScreamForMe);
        SubscribeLocalEvent<ImperialBlindSpellComponent, ImperialSpellEffectEvent>(OnBlind);
        SubscribeLocalEvent<ImperialBarnyardSpellComponent, ImperialSpellEffectEvent>(OnBarnyard);
        SubscribeLocalEvent<ImperialTieShoesSpellComponent, ImperialSpellEffectEvent>(OnTieShoes);
        SubscribeLocalEvent<ImperialMutateSpellComponent, ImperialSpellEffectEvent>(OnMutate);
        SubscribeLocalEvent<ImperialRodFormSpellComponent, ImperialSpellEffectEvent>(OnRodForm);
        SubscribeLocalEvent<ImperialConjureSpellComponent, ImperialSpellEffectEvent>(OnConjure);
        SubscribeLocalEvent<ImperialTeslaSpellComponent, ImperialSpellEffectEvent>(OnTesla);
        SubscribeLocalEvent<ImperialTeslaSpellComponent, ImperialSpellChannelDoAfterEvent>(OnTeslaCharged);
        SubscribeLocalEvent<ImperialLightningProjectileComponent, ProjectileHitEvent>(OnLightningHit);
        SubscribeLocalEvent<ImperialConjureItemSpellComponent, ImperialSpellEffectEvent>(OnConjureItem);
        SubscribeLocalEvent<ImperialInfiniteGunComponent, GunShotEvent>(OnInfiniteGunShot);
        SubscribeLocalEvent<ImperialSplattercastingSpellComponent, ImperialSpellEffectEvent>(OnSplattercasting);
        SubscribeLocalEvent<ImperialSplattercastingSpellComponent, ImperialSpellChannelDoAfterEvent>(OnSplattercastingDone);
        SubscribeLocalEvent<ImperialSplattercasterComponent, ImperialSpellCastEvent>(OnSplatterCast);
        SubscribeLocalEvent<ImperialSanguineStrikeSpellComponent, ImperialSpellEffectEvent>(OnSanguineStrike);
        SubscribeLocalEvent<ImperialSanguineEnchantedComponent, Content.Shared.Weapons.Melee.Events.MeleeHitEvent>(OnSanguineHit);
        SubscribeLocalEvent<ImperialSanguineEnchantedComponent, Content.Shared.Hands.GotUnequippedHandEvent>(OnSanguineDropped);
        SubscribeLocalEvent<ImperialWizardMutatedComponent, Content.Shared.Weapons.Melee.Events.GetMeleeDamageEvent>(OnHulkDamage);
        SubscribeLocalEvent<ImperialWizardMutatedComponent, ImperialLaserEyesActionEvent>(OnLaserEyes);
        SubscribeLocalEvent<ImperialShoeLacesComponent, Content.Shared.Inventory.Events.GotUnequippedEvent>(OnShoesRemoved);
        SubscribeLocalEvent<ImperialWizardRodComponent, Robust.Shared.Physics.Events.StartCollideEvent>(OnRodCollide);
    }

    #region Касания

    /// <summary>smite: вспышка вокруг и разрыв цели. Антимагия бьёт отдачей по магу.</summary>
    private void OnSmite(Entity<ImperialSmiteSpellComponent> ent, ref ImperialTouchHitEvent args)
    {
        args.Success = true;
        if (_antimagic.CanBlockMagic(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-smite-feedback", ("target", args.Target)), args.Caster, args.Caster, PopupType.LargeCaution);
            _damage.TryChangeDamage(args.Caster, new DamageSpecifier { DamageDict = { ["Blunt"] = 25 } }, origin: args.Caster);
            _flash.FlashArea(args.Caster, args.Caster, 7, TimeSpan.FromSeconds(5));
            return;
        }

        Spawn("EffectSparks", Transform(args.Target).Coordinates);
        _flash.FlashArea(args.Target, args.Caster, 7, TimeSpan.FromSeconds(5));
        Log.Info($"{ToPrettyString(args.Caster)} разорвал {ToPrettyString(args.Target)} заклинанием «Кара»");
        _gibbing.Gib(args.Target, user: args.Caster);
    }

    /// <summary>flesh_to_stone: Stun(4 с) и petrify() на 8 минут.</summary>
    private void OnFleshToStone(Entity<ImperialFleshToStoneSpellComponent> ent, ref ImperialTouchHitEvent args)
    {
        args.Success = true;
        if (_antimagic.CanBlockMagic(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-stone-blocked", ("target", args.Target)), args.Caster, args.Caster);
            _popup.PopupEntity(Loc.GetString("imperial-spell-stone-blocked-self"), args.Target, args.Target);
            return;
        }

        _stun.TryUpdateStunDuration(args.Target, TimeSpan.FromSeconds(4));
        if (HasComp<TurnedToStoneComponent>(args.Target))
            return;

        var stone = new TurnedToStoneComponent { LifeTime = ent.Comp.StatueTime };
        AddComp(args.Target, stone);
        _popup.PopupEntity(Loc.GetString("imperial-spell-stone-victim"), args.Target, args.Target, PopupType.LargeCaution);
    }

    /// <summary>scream_for_me: крик и раны по всему телу. Антимагия — рана руки мага.</summary>
    private void OnScreamForMe(Entity<ImperialScreamForMeSpellComponent> ent, ref ImperialTouchHitEvent args)
    {
        if (!HasComp<HumanoidProfileComponent>(args.Target))
            return;

        args.Success = true;
        var victim = _antimagic.CanBlockMagic(args.Target) ? args.Caster : args.Target;
        if (victim == args.Caster)
            _popup.PopupEntity(Loc.GetString("imperial-spell-scream-feedback"), args.Caster, args.Caster, PopupType.LargeCaution);
        else
            _chat.TryEmoteWithChat(victim, "Scream", ignoreActionBlocker: true);

        var damage = victim == args.Caster ? ent.Comp.Damage / 4 : ent.Comp.Damage;
        _damage.TryChangeDamage(victim, damage, origin: args.Caster);
        _blood.TryModifyBleedAmount(victim, victim == args.Caster ? ent.Comp.Bleed / 4 : ent.Comp.Bleed);
    }

    #endregion

    #region Направленные

    /// <summary>blind: только люди, которые ещё видят.</summary>
    private void OnBlind(Entity<ImperialBlindSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        if (args.Target is not { } target || target == args.Caster || !HasComp<HumanoidProfileComponent>(target)
            || TryComp<BlindableComponent>(target, out var blindable) && blindable.IsBlind)
            return;

        args.Handled = true;
        if (Blocked(args.Caster, target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-blind-blocked"), target, target);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-spell-blind-victim"), target, target, PopupType.MediumCaution);
        _status.TryAddStatusEffect<TemporaryBlindnessComponent>(target, TemporaryBlindnessSystem.BlindingStatusEffect,
            ent.Comp.Duration, true);
    }

    /// <summary>barnyardcurse: маска животного вместо лица.</summary>
    private void OnBarnyard(Entity<ImperialBarnyardSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        if (args.Target is not { } target || target == args.Caster || !HasComp<HumanoidProfileComponent>(target) || ent.Comp.Masks.Count == 0)
            return;

        if (_inventory.TryGetSlotEntity(target, "mask", out var worn) && HasComp<ImperialCursedMaskComponent>(worn))
            return;

        args.Handled = true;
        if (Blocked(args.Caster, target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-barnyard-blocked"), target, target);
            return;
        }

        var mask = Spawn(_random.Pick(ent.Comp.Masks), Transform(target).Coordinates);
        if (worn != null && !_inventory.TryUnequip(target, "mask", true, true))
            QueueDel(worn.Value);
        if (!_inventory.TryEquip(target, mask, "mask", true, true))
        {
            QueueDel(mask);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-spell-barnyard-victim", ("animal", Name(mask))), target, target, PopupType.LargeCaution);
        _flash.Flash(target, args.Caster, null, TimeSpan.FromSeconds(2), 0.8f, false);
    }

    /// <summary>untie_shoes: развязать, затем завязать узлом; с уровнем — шнурки на любой обуви и призыв обуви.</summary>
    private void OnTieShoes(Entity<ImperialTieShoesSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        if (args.Target is not { } target || !HasComp<MobStateComponent>(target) || !TryComp<ImperialSpellComponent>(ent, out var spell))
            return;

        args.Handled = true;
        if (Blocked(args.Caster, target, ImperialMagicResistanceAll))
            return;

        if (!HasComp<HumanoidProfileComponent>(target))
        {
            // Животным — магические путы: замедление на 3 × уровень секунд.
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(1));
            _popup.PopupEntity(Loc.GetString("imperial-spell-shoes-tied-animal", ("target", target)), args.Caster, args.Caster);
            return;
        }

        if (!_inventory.TryGetSlotEntity(target, "shoes", out var shoes))
        {
            if (spell.Level < 3)
            {
                _popup.PopupEntity(Loc.GetString("imperial-spell-shoes-none", ("target", target)), args.Caster, args.Caster);
                return;
            }

            var summoned = Spawn(ent.Comp.SummonedShoes, Transform(target).Coordinates);
            if (!_inventory.TryEquip(target, summoned, "shoes", true, true))
            {
                QueueDel(summoned);
                return;
            }

            shoes = summoned;
        }

        var laces = EnsureComp<ImperialShoeLacesComponent>(shoes.Value);
        switch (laces.State)
        {
            case ImperialLaceState.Tied:
                laces.State = ImperialLaceState.Untied;
                _popup.PopupEntity(Loc.GetString("imperial-spell-shoes-untied", ("target", target)), args.Caster, args.Caster);
                break;
            case ImperialLaceState.Untied:
                laces.State = ImperialLaceState.Knotted;
                _popup.PopupEntity(Loc.GetString("imperial-spell-shoes-knotted", ("target", target)), args.Caster, args.Caster);
                break;
            default:
                _popup.PopupEntity(Loc.GetString("imperial-spell-shoes-already", ("target", target)), args.Caster, args.Caster);
                break;
        }

        // after_cast(): цель далеко — откат в 10 раз дольше.
        var far = (_transform.GetWorldPosition(target) - _transform.GetWorldPosition(args.Caster)).Length() > 7
                  || Transform(target).MapID != Transform(args.Caster).MapID;
        if (far)
        {
            spell.SkipCooldown = true;
            Timer.Spawn(0, () => _actions.SetCooldown(ent.Owner, _spells.GetCooldown(spell) * 11));
        }
    }

    private const ImperialMagicResistance ImperialMagicResistanceAll = ImperialMagicResistance.All;

    /// <summary>Шнурки мешают ходить: развязанные — иногда спотыкаешься, связанные узлом — падаешь.</summary>
    private void UpdateLaces()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialShoeLacesComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var laces, out var xform))
        {
            if (laces.State == ImperialLaceState.Tied || now < laces.NextTrip || !HasComp<MobStateComponent>(xform.ParentUid))
                continue;

            var wearer = xform.ParentUid;
            if (!TryComp<PhysicsComponent>(wearer, out var physics) || physics.LinearVelocity.LengthSquared() < 1f)
                continue;

            laces.NextTrip = now + TimeSpan.FromSeconds(2);
            if (laces.State == ImperialLaceState.Untied && !_random.Prob(0.3f))
                continue;

            _stun.TryKnockdown(wearer, TimeSpan.FromSeconds(laces.State == ImperialLaceState.Knotted ? 3 : 1));
            _popup.PopupEntity(Loc.GetString(laces.State == ImperialLaceState.Knotted ? "imperial-spell-shoes-trip-knotted" : "imperial-spell-shoes-trip"),
                wearer, wearer, PopupType.MediumCaution);
        }
    }

    private void OnShoesRemoved(Entity<ImperialShoeLacesComponent> ent, ref Content.Shared.Inventory.Events.GotUnequippedEvent args)
    {
        // Сняв обувь, шнурки можно спокойно завязать.
        RemCompDeferred<ImperialShoeLacesComponent>(ent);
    }

    #endregion

    #region Себя

    /// <summary>mutate: халк, лазерные глаза и гигантизм на 30 секунд.</summary>
    private void OnMutate(Entity<ImperialMutateSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        if (!HasComp<HumanoidProfileComponent>(args.Caster))
            return;

        args.Handled = true;
        var mutated = EnsureComp<ImperialWizardMutatedComponent>(args.Caster);
        mutated.End = _timing.CurTime + ent.Comp.Duration;
        if (mutated.LaserAction == null)
            mutated.LaserAction = _actions.AddAction(args.Caster, ent.Comp.LaserAction);
        _popup.PopupEntity(Loc.GetString("imperial-spell-mutate"), args.Caster, args.Caster, PopupType.Medium);
        Dirty(args.Caster, mutated);
    }

    private void OnHulkDamage(Entity<ImperialWizardMutatedComponent> ent, ref Content.Shared.Weapons.Melee.Events.GetMeleeDamageEvent args)
    {
        // hulk: удары 15 грубого урона.
        args.Damage += new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } };
    }

    /// <summary>laser_eyes: лазер из глаз в точку клика.</summary>
    private void OnLaserEyes(Entity<ImperialWizardMutatedComponent> ent, ref ImperialLaserEyesActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var from = _transform.GetMapCoordinates(ent);
        var to = _transform.ToMapCoordinates(args.Target);
        var direction = to.Position - from.Position;
        if (from.MapId != to.MapId || direction.LengthSquared() < 0.01f)
            return;

        var laser = Spawn("BulletLaser", from);
        _guns.ShootProjectile(laser, direction.Normalized(), Vector2.Zero, ent, ent, 25f);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/laser.ogg"), ent);
    }

    private void UpdateMutations()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialWizardMutatedComponent>();
        var expired = new List<(EntityUid, ImperialWizardMutatedComponent)>();
        while (query.MoveNext(out var uid, out var mutated))
        {
            if (now >= mutated.End)
                expired.Add((uid, mutated));
        }

        foreach (var (uid, mutated) in expired)
        {
            if (mutated.LaserAction != null)
                _actions.RemoveAction(uid, mutated.LaserAction);
            RemComp<ImperialWizardMutatedComponent>(uid);
            _popup.PopupEntity(Loc.GetString("imperial-spell-mutate-end"), uid, uid);
        }
    }

    /// <summary>rod_form: стержень в направлении взгляда; дальность и урон растут с уровнем.</summary>
    private void OnRodForm(Entity<ImperialRodFormSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        if (!TryComp<ImperialSpellComponent>(ent, out var spell))
            return;

        args.Handled = true;
        var caster = args.Caster;
        var coords = _transform.GetMapCoordinates(caster);
        var direction = _transform.GetWorldRotation(caster).ToWorldVec();
        var rod = Spawn(ent.Comp.Rod, coords);
        var comp = EnsureComp<ImperialWizardRodComponent>(rod);
        comp.Wizard = caster;
        comp.Start = coords.Position;
        comp.MaxDistance = ent.Comp.BaseDistance + ent.Comp.DistancePerRank * (spell.Level - 1);
        comp.Damage = 70 + ent.Comp.DamagePerRank * (spell.Level - 1);
        comp.Hit.Add(caster);

        var container = _containers.EnsureContainer<ContainerSlot>(rod, RodContainer);
        _containers.Insert(caster, container);
        _transform.SetWorldRotation(rod, direction.ToWorldAngle());
        if (TryComp<PhysicsComponent>(rod, out var physics))
            _physics.SetLinearVelocity(rod, direction * 15f, body: physics);
    }

    /// <summary>penetrate(): пробитым 70 + бонус урона; антимагия останавливает стержень.</summary>
    private void OnRodCollide(Entity<ImperialWizardRodComponent> ent, ref Robust.Shared.Physics.Events.StartCollideEvent args)
    {
        var target = args.OtherEntity;
        if (!HasComp<MobStateComponent>(target) || !ent.Comp.Hit.Add(target))
            return;

        if (_antimagic.CanBlockMagic(target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-rod-blocked", ("target", target)), target, PopupType.MediumCaution);
            StopRod(ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-spell-rod-hit", ("target", target)), target, PopupType.LargeCaution);
        _damage.TryChangeDamage(target, new DamageSpecifier { DamageDict = { ["Blunt"] = ent.Comp.Damage } }, true, origin: ent.Comp.Wizard);
    }

    private void UpdateRods()
    {
        var query = EntityQueryEnumerator<ImperialWizardRodComponent, TransformComponent>();
        var stop = new List<Entity<ImperialWizardRodComponent>>();
        while (query.MoveNext(out var uid, out var rod, out var xform))
        {
            if ((_transform.GetWorldPosition(xform) - rod.Start).Length() >= rod.MaxDistance
                || TryComp<PhysicsComponent>(uid, out var physics) && physics.LinearVelocity.LengthSquared() < 1f)
                stop.Add((uid, rod));
        }

        foreach (var rod in stop)
        {
            StopRod(rod);
        }
    }

    /// <summary>stop_travel(): маг выходит из стержня.</summary>
    private void StopRod(Entity<ImperialWizardRodComponent> rod)
    {
        if (_containers.TryGetContainer(rod, RodContainer, out var container))
            _containers.EmptyContainer(container, true, Transform(rod).Coordinates);
        QueueDel(rod);
    }

    #endregion

    #region Призывы

    /// <summary>conjure: призвать существ на случайных клетках вокруг.</summary>
    private void OnConjure(Entity<ImperialConjureSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        if (!TryComp<ImperialSpellComponent>(ent, out var spell))
            return;

        args.Handled = true;
        var center = Transform(args.Caster).Coordinates;
        var amount = ent.Comp.Amount + ent.Comp.AmountPerLevel * (spell.Level - 1);
        var tiles = FreeTilesAround(center, ent.Comp.Radius, ent.Comp.RespectsDensity);
        for (var i = 0; i < amount && tiles.Count > 0; i++)
        {
            var tile = _random.PickAndTake(tiles);
            var summoned = Spawn(_random.Pick(ent.Comp.Summons), tile);
            if (ent.Comp.Lifespan > TimeSpan.Zero)
                EnsureComp<Robust.Shared.Spawners.TimedDespawnComponent>(summoned).Lifetime = (float) ent.Comp.Lifespan.TotalSeconds;

            if (ent.Comp.Faction is { } faction)
            {
                _faction.ClearFactions(summoned);
                _faction.AddFaction(summoned, faction);
            }
        }

        // Заклинатель на минуту свой для призванных (simian: faction monkey).
        if (ent.Comp.Faction is { } ally && TryComp<NpcFactionMemberComponent>(args.Caster, out _) && !_faction.IsMember(args.Caster, ally))
        {
            _faction.AddFaction(args.Caster, ally);
            _temporaryAllies.Add((args.Caster, ally, _timing.CurTime + ent.Comp.AllyTime));
        }
    }

    /// <summary>Клетки в радиусе: без космоса и (по желанию) без стен.</summary>
    public List<EntityCoordinates> FreeTilesAround(EntityCoordinates center, float radius, bool respectDensity)
    {
        var result = new List<EntityCoordinates>();
        if (_transform.GetGrid(center) is not { } gridUid || !TryComp<Robust.Shared.Map.Components.MapGridComponent>(gridUid, out var grid))
        {
            result.Add(center);
            return result;
        }

        var maps = EntityManager.System<SharedMapSystem>();
        var turf = EntityManager.System<Content.Shared.Maps.TurfSystem>();
        var origin = maps.LocalToTile(gridUid, grid, center);
        var r = (int) radius;
        for (var x = -r; x <= r; x++)
        for (var y = -r; y <= r; y++)
        {
            var pos = origin + new Vector2i(x, y);
            if (!maps.TryGetTileRef(gridUid, grid, pos, out var tileRef) || turf.IsSpace(tileRef))
                continue;

            if (respectDensity && turf.IsTileBlocked(tileRef, Content.Shared.Physics.CollisionGroup.Impassable))
                continue;

            result.Add(maps.GridTileToLocal(gridUid, grid, pos));
        }

        return result;
    }

    private void UpdateAllies()
    {
        var now = _timing.CurTime;
        for (var i = _temporaryAllies.Count - 1; i >= 0; i--)
        {
            var (mob, faction, end) = _temporaryAllies[i];
            if (now < end)
                continue;

            _temporaryAllies.RemoveAt(i);
            if (!TerminatingOrDeleted(mob))
                _faction.RemoveFaction(mob, faction);
        }
    }

    /// <summary>conjure_item: предмет в руку (прежний — в мусор).</summary>
    private void OnConjureItem(Entity<ImperialConjureItemSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        var caster = args.Caster;
        if (ent.Comp.RequiresHands && !_hands.TryGetEmptyHand(caster, out _))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-no-free-hands"), caster, caster);
            return;
        }

        args.Handled = true;
        if (ent.Comp.DeleteOld)
        {
            foreach (var old in ent.Comp.Items)
            {
                QueueDel(old);
            }

            ent.Comp.Items.Clear();
        }

        var item = Spawn(ent.Comp.Item, Transform(caster).Coordinates);
        ent.Comp.Items.Add(item);
        _hands.TryPickupAnyHand(caster, item);
    }

    /// <summary>process_fire(): зачарованная винтовка улетает, в руке — новая; у залпа — заряды.</summary>
    private void OnInfiniteGunShot(Entity<ImperialInfiniteGunComponent> ent, ref GunShotEvent args)
    {
        var user = args.User;
        if (ent.Comp.Barrage)
        {
            ent.Comp.GunsLeft--;
            if (ent.Comp.GunsLeft <= 0)
                QueueDel(ent);
            return;
        }

        if (ent.Comp.GunsLeft > 0 && MetaData(ent).EntityPrototype is { } proto)
        {
            var next = Spawn(proto.ID, Transform(user).Coordinates);
            EnsureComp<ImperialInfiniteGunComponent>(next).GunsLeft = ent.Comp.GunsLeft - 1;
            _hands.TryDrop(user, ent.Owner, checkActionBlocker: false);
            _transform.SetWorldPosition(ent, _transform.GetWorldPosition(user) + _random.NextVector2(2f));
            _hands.TryPickupAnyHand(user, next);
        }
        else
        {
            _hands.TryDrop(user, ent.Owner, checkActionBlocker: false);
        }

        EnsureComp<Robust.Shared.Spawners.TimedDespawnComponent>(ent).Lifetime = 10f;
    }

    #endregion

    #region Тесла и молния

    /// <summary>charged/beam/tesla: 10 секунд накопления, затем разряд по случайной ближайшей цели.</summary>
    private void OnTesla(Entity<ImperialTeslaSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        if (ent.Comp.Channeling || !TryComp<ImperialSpellComponent>(ent, out var spell))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-already-channeling"), args.Caster, args.Caster);
            return;
        }

        args.Handled = true;
        spell.SkipCooldown = true;
        var doAfter = new DoAfterArgs(EntityManager, args.Caster, ent.Comp.ChannelTime, new ImperialSpellChannelDoAfterEvent(), ent, args.Caster)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            NeedHand = false,
        };
        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        ent.Comp.Channeling = true;
        _popup.PopupEntity(Loc.GetString("imperial-spell-tesla-channel"), args.Caster, args.Caster);
        _audio.PlayPvs(ChargeUpSound, args.Caster);
    }

    private void OnTeslaCharged(Entity<ImperialTeslaSpellComponent> ent, ref ImperialSpellChannelDoAfterEvent args)
    {
        ent.Comp.Channeling = false;
        if (args.Cancelled || !TryComp<ImperialSpellComponent>(ent, out var spell))
            return;

        var caster = args.User;
        var first = RandomTarget(caster, caster, ent.Comp.Radius);
        if (first == null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-no-targets"), caster, caster);
            return;
        }

        _spells.Invoke((ent, spell), caster);
        if (spell.CastSound != null)
            _audio.PlayPvs(spell.CastSound, caster);
        _actions.SetCooldown(ent.Owner, _spells.GetCooldown(spell));

        // send_beam(): цепь до 5 прыжков, каждый слабее на 5.
        var from = caster;
        var to = first.Value;
        var energy = ent.Comp.Energy;
        for (var bounce = ent.Comp.Bounces; ; bounce--)
        {
            _lightning.ShootLightning(from, to, "Lightning", triggerLightningEvents: false);
            _audio.PlayPvs(ShockSound, to);
            if (_antimagic.CanBlockMagic(to))
                _popup.PopupEntity(Loc.GetString("imperial-spell-tesla-absorb", ("target", to)), to, PopupType.Medium);
            else
                _electrocution.TryDoElectrocution(to, null, energy, TimeSpan.FromSeconds(2), true, ignoreInsulation: true);

            energy -= ent.Comp.EnergyLostPerBounce;
            if (bounce < 1 || energy <= 0 || RandomTarget(caster, to, ent.Comp.Radius) is not { } next)
                break;

            from = to;
            to = next;
        }
    }

    /// <summary>get_target(): случайное существо рядом (не сам маг и не центр).</summary>
    private EntityUid? RandomTarget(EntityUid caster, EntityUid center, float radius)
    {
        var candidates = _lookup.GetEntitiesInRange<MobStateComponent>(Transform(center).Coordinates, radius)
            .Where(m => m.Owner != caster && m.Owner != center && !_mobs.IsDead(m))
            .Select(m => m.Owner)
            .ToList();
        return candidates.Count == 0 ? null : _random.Pick(candidates);
    }

    /// <summary>lightning: попадание — цепная молния tesla_zap.</summary>
    private void OnLightningHit(Entity<ImperialLightningProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        _lightning.ShootRandomLightnings(args.Target, ent.Comp.Range, ent.Comp.Bolts, "Lightning", 1, false);
    }

    #endregion

    #region Кровавая магия

    /// <summary>splattercasting: 5 секунд сосредоточения, затем маг становится вампиром навсегда.</summary>
    private void OnSplattercasting(Entity<ImperialSplattercastingSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("imperial-spell-splatter-focus"), args.Caster, args.Caster);
        var doAfter = new DoAfterArgs(EntityManager, args.Caster, TimeSpan.FromSeconds(5), new ImperialSpellChannelDoAfterEvent(), ent, args.Caster)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnSplattercastingDone(Entity<ImperialSplattercastingSpellComponent> ent, ref ImperialSpellChannelDoAfterEvent args)
    {
        var caster = args.User;
        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-splatter-broken"), caster, caster);
            return;
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/pope_entry.ogg"), caster);
        _popup.PopupEntity(Loc.GetString("imperial-spell-splatter-done"), caster, caster, PopupType.LargeCaution);
        EnsureComp<ImperialSplattercasterComponent>(caster).NextDrain = _timing.CurTime + TimeSpan.FromSeconds(10);

        // Красная мантия вместо обычной.
        foreach (var (slot, proto) in new[] { ("outerClothing", "ClothingOuterWizardRed"), ("head", "ClothingHeadHatRedwizard"), ("jumpsuit", "ClothingUniformJumpsuitColorRed") })
        {
            _inventory.TryUnequip(caster, slot, true, true);
            var item = Spawn(proto, Transform(caster).Coordinates);
            if (!_inventory.TryEquip(caster, item, slot, true, true))
                QueueDel(item);
        }

        _actions.RemoveAction(ent.Owner);
        QueueDel(ent);
    }

    /// <summary>on_after_spell_cast(): откат в 10 раз короче, разница × 0,5 — кровью.</summary>
    private void OnSplatterCast(Entity<ImperialSplattercasterComponent> ent, ref ImperialSpellCastEvent args)
    {
        if (!TryComp<ImperialSpellComponent>(args.Spell, out var spell) || spell.School == "sanguine"
            || !TryComp<Content.Shared.Actions.Components.ActionComponent>(args.Spell, out var action) || action.Cooldown is not { } cd)
            return;

        var remaining = cd.End - _timing.CurTime;
        if (remaining <= TimeSpan.Zero)
            return;

        var reduced = remaining / 10;
        var cost = (float) (remaining - reduced).TotalSeconds * 10f * 0.5f;
        _actions.SetCooldown(args.Spell, reduced);
        _blood.TryModifyBloodLevel(ent.Owner, -cost / 10f);
        var desc = cost switch
        {
            <= 50 => "imperial-spell-splatter-trickle",
            <= 100 => "imperial-spell-splatter-stream",
            <= 200 => "imperial-spell-splatter-river",
            _ => "imperial-spell-splatter-torrent",
        };
        _popup.PopupEntity(Loc.GetString(desc), ent, ent, PopupType.SmallCaution);
    }

    private void UpdateSplatter()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialSplattercasterComponent>();
        while (query.MoveNext(out var uid, out var splatter))
        {
            if (now < splatter.NextDrain)
                continue;

            splatter.NextDrain = now + TimeSpan.FromSeconds(10);
            _blood.TryModifyBloodLevel(uid, -1f);
        }
    }

    /// <summary>sanguine_strike: удвоить урон оружия в руке до следующего удара.</summary>
    private void OnSanguineStrike(Entity<ImperialSanguineStrikeSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        var caster = args.Caster;
        if (!_hands.TryGetActiveItem(caster, out var item) || !HasComp<Content.Shared.Weapons.Melee.MeleeWeaponComponent>(item))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-sanguine-need-weapon"), caster, caster);
            return;
        }

        args.Handled = true;
        EnsureComp<ImperialSanguineEnchantedComponent>(item.Value).Spell = ent;
        _popup.PopupEntity(Loc.GetString("imperial-spell-sanguine-glow", ("item", item.Value)), caster, caster);
        if (TryComp<ImperialSpellComponent>(ent, out var spell))
            spell.SkipCooldown = true;
    }

    private void OnSanguineHit(Entity<ImperialSanguineEnchantedComponent> ent, ref Content.Shared.Weapons.Melee.Events.MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        // force = min(force × 2, force + 20) и вампиризм.
        var bonus = new DamageSpecifier(args.BaseDamage);
        var total = bonus.GetTotal().Float();
        var extra = Math.Min(total, 20f);
        args.BonusDamage += bonus * (extra / Math.Max(1f, total));

        var target = args.HitEntities[0];
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/charge.ogg"), target);
        _blood.TryModifyBloodLevel(target, -50);
        _blood.TryModifyBloodLevel(args.User, 50);
        _damage.TryChangeDamage(args.User, new DamageSpecifier { DamageDict = { ["Blunt"] = -extra / 2, ["Slash"] = -extra / 2 } }, true);
        EndSanguine(ent);
    }

    private void OnSanguineDropped(Entity<ImperialSanguineEnchantedComponent> ent, ref Content.Shared.Hands.GotUnequippedHandEvent args)
    {
        _popup.PopupEntity(Loc.GetString("imperial-spell-sanguine-fade", ("item", ent.Owner)), args.User, args.User);
        EndSanguine(ent);
    }

    private void EndSanguine(Entity<ImperialSanguineEnchantedComponent> ent)
    {
        if (TryComp<ImperialSpellComponent>(ent.Comp.Spell, out var spell))
            _actions.SetCooldown(ent.Comp.Spell, _spells.GetCooldown(spell));
        RemCompDeferred<ImperialSanguineEnchantedComponent>(ent);
    }

    #endregion

    private void UpdateOffensive()
    {
        UpdateLaces();
        UpdateMutations();
        UpdateRods();
        UpdateAllies();
        UpdateSplatter();
    }
}

/// <summary>Проклятая маска животного (не снимается).</summary>
[RegisterComponent]
public sealed partial class ImperialCursedMaskComponent : Component;
