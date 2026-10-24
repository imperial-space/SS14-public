using System.Linq;
using System.Numerics;
using Content.Server.Polymorph.Systems;
using Content.Shared.Administration.Systems;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Doors.Components;
using Content.Shared.Examine;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Storage.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>Эффект магического снаряда (/obj/projectile/magic/*).</summary>
public enum ImperialMagicBolt : byte
{
    Nothing,
    Death,
    Resurrection,
    Teleport,
    Safety,
    Door,
    Change,
    Animate,
    Locker,
    Flying,
    Fetch,
    Antimagic,
    Babel,
    Necropotence,
    Fireball,
    Shrink,
    Chill,
    Freeze,
    Swap,
    Tentacle,
    Booze,
    Hallucination,
    Pax,
    Pizza,
    Prank,
    Repulse,
    Zap,
    Plague,
    Bald,
    Levitate,
    Rebellion,
    Bounty,
    Wipe,

    /// <summary>Посох хаоса: случайный эффект из списка allowed_projectile_types.</summary>
    Chaos,
}

/// <summary>Магический снаряд: антимагия гасит его, иначе срабатывает эффект.</summary>
[RegisterComponent]
public sealed partial class ImperialMagicBoltComponent : Component
{
    [DataField]
    public ImperialMagicBolt Effect;
}

/// <summary>
/// /obj/item/gun/magic/staff: посох мага. Чужак (не маг) может пострадать сам;
/// «не в логове» — нельзя стрелять в логове волшебника.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialMagicStaffComponent : Component
{
    [DataField]
    public ImperialStaffIntruder Intruder;

    /// <summary>allow_intruder_use.</summary>
    [DataField]
    public bool AllowIntruder;

    /// <summary>no_den_usage.</summary>
    [DataField]
    public bool NoDenUsage;
}

public enum ImperialStaffIntruder : byte
{
    None,

    /// <summary>Посох изменения превращает самого чужака.</summary>
    Change,

    /// <summary>Посох хаоса: 5% — выстрел в себя.</summary>
    Chaos,
}

/// <summary>/obj/item/gun/magic/wand: палочка без самозарядки, «выстрел в себя» — особый эффект.</summary>
[RegisterComponent]
public sealed partial class ImperialWandComponent : Component
{
    /// <summary>variable_charges: 25% — полный заряд, 50% — половина, 25% — треть.</summary>
    [DataField]
    public bool VariableCharges = true;

    /// <summary>zap_self(): эффект на себя.</summary>
    [DataField]
    public ImperialMagicBolt SelfEffect;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>COMSIG_ITEM_MAGICALLY_CHARGED.</summary>
[ByRefEvent]
public record struct ImperialMagicChargeEvent(EntityUid Caster, bool Charged = false, bool BurntOut = false);

/// <summary>Уменьшение (/datum/component/shrink).</summary>
[RegisterComponent]
public sealed partial class ImperialShrinkTimerComponent : Component
{
    [ViewVariables]
    public TimeSpan End;
}

/// <summary>Проклятый шкаф (/obj/structure/closet/decay): через 5 минут распадается.</summary>
[RegisterComponent]
public sealed partial class ImperialDecayLockerComponent : Component
{
    [ViewVariables]
    public TimeSpan BustAt;
}

public sealed partial class ImperialWizardSpellsSystem
{
    private static readonly ProtoId<Content.Shared.Tag.TagPrototype> WallTagId = "Wall";

    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly ImperialWizardRuleSystem _wizardRule = default!;
    [Dependency] private readonly Content.Shared.Mobs.Systems.MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly Content.Server.Chemistry.Containers.EntitySystems.SolutionContainerSystem _solutions = default!;

    /// <summary>wabbajack(): во что превращает посох изменения.</summary>
    private static readonly EntProtoId[] WabbajackForms =
    {
        "MobMonkey", "MobCorgi", "MobCat", "MobMouse", "MobGoat", "MobBearSpace", "MobCarp", "MobGorilla", "MobChicken",
        "MobPig", "MobCow", "MobFox", "MobLizard", "MobSpiderSpace", "MobHuman", "MobReptilian", "MobMoth", "MobSlimePerson",
        "MobDwarf", "MobVox", "MobArachnid", "MobDiona",
    };

    private static readonly ImperialMagicBolt[] ChaosEffects =
    {
        ImperialMagicBolt.Animate, ImperialMagicBolt.Antimagic, ImperialMagicBolt.Babel, ImperialMagicBolt.Change, ImperialMagicBolt.Death,
        ImperialMagicBolt.Door, ImperialMagicBolt.Fetch, ImperialMagicBolt.Fireball, ImperialMagicBolt.Flying, ImperialMagicBolt.Freeze,
        ImperialMagicBolt.Levitate, ImperialMagicBolt.Locker, ImperialMagicBolt.Necropotence, ImperialMagicBolt.Plague,
        ImperialMagicBolt.Rebellion, ImperialMagicBolt.Resurrection, ImperialMagicBolt.Shrink, ImperialMagicBolt.Swap,
        ImperialMagicBolt.Teleport, ImperialMagicBolt.Tentacle, ImperialMagicBolt.Wipe, ImperialMagicBolt.Chill, ImperialMagicBolt.Bounty,
    };

    private static readonly EntProtoId[] DoorTypes =
    {
        "WoodDoor", "MetalDoor", "SilverDoor", "GoldDoor", "UraniumDoor", "SandstoneDoor", "PlasmaDoor", "DiamondDoor",
    };

    private void InitializeMagicGuns()
    {
        SubscribeLocalEvent<ImperialMagicBoltComponent, ProjectileHitEvent>(OnBoltHit);
        SubscribeLocalEvent<ImperialMagicStaffComponent, AttemptShootEvent>(OnStaffAttemptShoot);
        SubscribeLocalEvent<ImperialWandComponent, MapInitEvent>(OnWandInit);
        SubscribeLocalEvent<ImperialWandComponent, UseInHandEvent>(OnWandZapSelf);
        SubscribeLocalEvent<ImperialWandComponent, ExaminedEvent>(OnWandExamined);
        SubscribeLocalEvent<BasicEntityAmmoProviderComponent, ImperialMagicChargeEvent>(OnGunCharged);
        SubscribeLocalEvent<ImperialSpellbookComponent, ImperialMagicChargeEvent>(OnSpellbookCharged);
    }

    #region Посохи и палочки

    /// <summary>process_fire(): логово, антимагия, чужак.</summary>
    private void OnStaffAttemptShoot(Entity<ImperialMagicStaffComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        var user = args.User;
        if (ent.Comp.NoDenUsage)
        {
            if (_wizardRule.InDen(user))
            {
                args.Cancelled = true;
                args.Message = Loc.GetString("imperial-staff-no-den", ("staff", ent.Owner));
                return;
            }

            ent.Comp.NoDenUsage = false;
        }

        if (!_antimagic.CanCastMagic(user))
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.AllowIntruder || HasComp<ImperialMagicallyGiftedComponent>(user))
            return;

        switch (ent.Comp.Intruder)
        {
            case ImperialStaffIntruder.Change:
                args.Cancelled = true;
                _hands.TryDrop(user, ent.Owner, checkActionBlocker: false);
                ApplyBolt(ImperialMagicBolt.Change, user, user, Transform(user).Coordinates, Vector2.Zero);
                break;
            case ImperialStaffIntruder.Chaos when _random.Prob(0.05f):
                args.Cancelled = true;
                _popup.PopupEntity(Loc.GetString("imperial-staff-chaos-backfire"), user, user, PopupType.LargeCaution);
                _hands.TryDrop(user, ent.Owner, checkActionBlocker: false);
                ApplyBolt(ImperialMagicBolt.Chaos, user, user, Transform(user).Coordinates, Vector2.Zero);
                break;
        }
    }

    /// <summary>Палочки: случайный запас зарядов (100 → 100/50/50/34%).</summary>
    private void OnWandInit(Entity<ImperialWandComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.VariableCharges || !TryComp<BasicEntityAmmoProviderComponent>(ent, out var ammo) || ammo.Capacity is not { } capacity)
            return;

        var roll = _random.Next(4);
        var max = roll switch
        {
            0 => capacity,
            3 => (int) Math.Ceiling(capacity / 3f),
            _ => (int) Math.Ceiling(capacity / 2f),
        };
        ammo.Capacity = max;
        _guns.UpdateBasicEntityAmmoCount((ent, ammo), max);
    }

    private void OnWandExamined(Entity<ImperialWandComponent> ent, ref ExaminedEvent args)
    {
        if (TryComp<BasicEntityAmmoProviderComponent>(ent, out var ammo))
            args.PushMarkup(Loc.GetString("imperial-wand-charges", ("charges", ammo.Count ?? 0)));
    }

    /// <summary>zap_self(): применить палочку на себя.</summary>
    private void OnWandZapSelf(Entity<ImperialWandComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !TryComp<BasicEntityAmmoProviderComponent>(ent, out var ammo))
            return;

        args.Handled = true;
        if (ammo.Count is <= 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-wand-empty", ("wand", ent.Owner)), args.User, args.User);
            return;
        }

        _guns.UpdateBasicEntityAmmoCount((ent, ammo), (ammo.Count ?? 1) - 1);
        if (ent.Comp.Sound != null)
            _audio.PlayPvs(ent.Comp.Sound, args.User);
        _popup.PopupEntity(Loc.GetString("imperial-wand-zap-self", ("wand", ent.Owner)), args.User, args.User, PopupType.MediumCaution);
        ApplyBolt(ent.Comp.SelfEffect, args.User, args.User, Transform(args.User).Coordinates, Vector2.Zero);
    }

    /// <summary>on_magic_charge(): заряды восстановлены; палочки при этом изнашиваются (80% — минус один максимум).</summary>
    private void OnGunCharged(Entity<BasicEntityAmmoProviderComponent> ent, ref ImperialMagicChargeEvent args)
    {
        if (ent.Comp.Capacity is not { } capacity)
            return;

        args.Charged = true;
        if (HasComp<ImperialWandComponent>(ent) && _random.Prob(0.8f))
            capacity--;

        if (capacity <= 0)
        {
            capacity = 0;
            args.BurntOut = true;
        }

        ent.Comp.Capacity = capacity;
        _guns.UpdateBasicEntityAmmoCount((ent, ent.Comp), capacity);
    }

    private void OnSpellbookCharged(Entity<ImperialSpellbookComponent> ent, ref ImperialMagicChargeEvent args)
    {
        args.BurntOut = true;
        _popup.PopupEntity(Loc.GetString("imperial-spellbook-charge-joke-" + _random.Next(1, 6)), args.Caster, args.Caster, PopupType.LargeCaution);
    }

    #endregion

    #region Снаряды

    private void OnBoltHit(Entity<ImperialMagicBoltComponent> ent, ref ProjectileHitEvent args)
    {
        var target = args.Target;
        if (HasComp<MobStateComponent>(target) && _antimagic.CanBlockMagic(target))
        {
            args.Damage = new DamageSpecifier();
            _popup.PopupEntity(Loc.GetString("imperial-bolt-fizzles", ("target", target)), target, PopupType.Small);
            return;
        }

        var velocity = TryComp<Robust.Shared.Physics.Components.PhysicsComponent>(ent, out var physics) ? physics.LinearVelocity : Vector2.Zero;
        ApplyBolt(ent.Comp.Effect, target, args.Shooter, Transform(ent).Coordinates, velocity);
    }

    /// <summary>on_hit() каждого вида магического снаряда.</summary>
    public void ApplyBolt(ImperialMagicBolt effect, EntityUid target, EntityUid? shooter, EntityCoordinates impact, Vector2 velocity)
    {
        if (effect == ImperialMagicBolt.Chaos)
            effect = _random.Pick(ChaosEffects);

        var isMob = HasComp<MobStateComponent>(target);
        switch (effect)
        {
            case ImperialMagicBolt.Death when isMob:
                if (HasComp<ImperialLichComponent>(target))
                {
                    _rejuvenate.PerformRejuvenate(target);
                    _popup.PopupEntity(Loc.GetString("imperial-bolt-undead-rise"), target, target);
                    break;
                }

                _mobs.ChangeMobState(target, MobState.Dead, origin: shooter);
                break;
            case ImperialMagicBolt.Resurrection when isMob:
                if (HasComp<ImperialLichComponent>(target))
                {
                    _mobs.ChangeMobState(target, MobState.Dead, origin: shooter);
                    break;
                }

                var wasDead = _mobs.IsDead(target);
                _rejuvenate.PerformRejuvenate(target);
                _popup.PopupEntity(Loc.GetString(wasDead ? "imperial-bolt-rise" : "imperial-bolt-feel-great"), target, target);
                break;
            case ImperialMagicBolt.Teleport:
                if (Transform(target).Anchored)
                    break;
                var tiles = FreeTilesAround(Transform(target).Coordinates, 10f, true);
                if (tiles.Count > 0)
                {
                    Spawn("WizardSmoke", Transform(target).Coordinates);
                    _transform.SetCoordinates(target, _random.Pick(tiles));
                    _transform.AttachToGridOrMap(target);
                }
                break;
            case ImperialMagicBolt.Safety when isMob:
                var areas = _teleport.GetAreas();
                if (areas.Count > 0 && _teleport.TryGetDestination(_random.Pick(areas.Values.ToList()), out var safe))
                {
                    Spawn("WizardSmoke", Transform(target).Coordinates);
                    _transform.SetCoordinates(target, safe);
                    _transform.AttachToGridOrMap(target);
                    Spawn("WizardSmoke", safe);
                }
                break;
            case ImperialMagicBolt.Door:
                MagicDoor(target);
                break;
            case ImperialMagicBolt.Change when isMob:
                Wabbajack(target);
                break;
            case ImperialMagicBolt.Animate when !isMob:
                AnimateObject(target, shooter);
                break;
            case ImperialMagicBolt.Locker when isMob:
                CursedLocker(target, impact);
                break;
            case ImperialMagicBolt.Flying when isMob && velocity.LengthSquared() > 0:
                _throwing.TryThrow(target, velocity.Normalized() * 30f, 12f, shooter, recoil: false);
                break;
            case ImperialMagicBolt.Fetch when isMob && shooter != null:
                var toShooter = _transform.GetWorldPosition(shooter.Value) - _transform.GetWorldPosition(target);
                _throwing.TryThrow(target, toShooter, 12f, shooter, recoil: false);
                break;
            case ImperialMagicBolt.Repulse when isMob && velocity.LengthSquared() > 0:
                _throwing.TryThrow(target, velocity.Normalized() * 7f, 10f, shooter, recoil: false);
                _stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(2));
                break;
            case ImperialMagicBolt.Antimagic when isMob:
                _popup.PopupEntity(Loc.GetString("imperial-bolt-antimagic"), target, target);
                EnsureComp<Content.Shared.Imperial.Antimagic.ImperialHolyWaterComponent>(target);
                break;
            case ImperialMagicBolt.Babel when isMob:
                EnsureComp<Content.Server.Speech.Components.ScrambledAccentComponent>(target);
                _popup.PopupEntity(Loc.GetString("imperial-bolt-babel"), target, target, PopupType.MediumCaution);
                break;
            case ImperialMagicBolt.Necropotence when isMob:
                SoulTap(target);
                break;
            case ImperialMagicBolt.Fireball:
                _damage.TryChangeDamage(target, new DamageSpecifier { DamageDict = { ["Heat"] = 10 } }, origin: shooter);
                EntityManager.System<Content.Server.Explosion.EntitySystems.ExplosionSystem>()
                    .QueueExplosion(_transform.ToMapCoordinates(impact), "Default", 20, 4, 6, shooter);
                break;
            case ImperialMagicBolt.Shrink:
                Shrink(target, TimeSpan.FromSeconds(_random.Next(60, 91)));
                break;
            case ImperialMagicBolt.Chill when isMob:
            case ImperialMagicBolt.Freeze when isMob:
                if (TryComp<Content.Shared.Temperature.Components.TemperatureComponent>(target, out var temperature))
                    _temperature.ForceChangeTemperature(target, Math.Max(2.7f, temperature.CurrentTemperature - 200), temperature);
                if (effect == ImperialMagicBolt.Freeze)
                    _stun.TryUpdateStunDuration(target, TimeSpan.FromSeconds(4));
                break;
            case ImperialMagicBolt.Swap when isMob && shooter != null:
                var a = _transform.GetMapCoordinates(target);
                var b = _transform.GetMapCoordinates(shooter.Value);
                _transform.SetMapCoordinates(target, b);
                _transform.SetMapCoordinates(shooter.Value, a);
                _audio.PlayPvs(SwapSound, target);
                break;
            case ImperialMagicBolt.Tentacle when isMob:
            case ImperialMagicBolt.Levitate when isMob:
            case ImperialMagicBolt.Rebellion when isMob:
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(6));
                break;
            case ImperialMagicBolt.Booze when isMob:
                if (_solutions.TryGetSolution(target, "chemicals", out var chem, out _))
                    _solutions.TryAddReagent(chem.Value, "Ethanol", 15, out _);
                break;
            case ImperialMagicBolt.Hallucination when isMob:
                _statusNew.TryAddStatusEffectDuration(target, "StatusEffectSeeingRainbow", TimeSpan.FromSeconds(30));
                break;
            case ImperialMagicBolt.Pax when isMob:
                _status.TryAddStatusEffect<PacifiedComponent>(target, "Pacified", TimeSpan.FromSeconds(30), true);
                break;
            case ImperialMagicBolt.Pizza:
                Spawn("FoodPizzaMargheritaSlice", impact);
                break;
            case ImperialMagicBolt.Prank when isMob:
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(3));
                Spawn("TrashBananaPeel", Transform(target).Coordinates);
                break;
            case ImperialMagicBolt.Zap when isMob:
                _electrocution.TryDoElectrocution(target, null, 20, TimeSpan.FromSeconds(2), true, ignoreInsulation: true);
                break;
            case ImperialMagicBolt.Plague when isMob:
                _damage.TryChangeDamage(target, new DamageSpecifier { DamageDict = { ["Poison"] = 20 } }, origin: shooter);
                break;
            case ImperialMagicBolt.Bald when isMob:
            case ImperialMagicBolt.Bounty when isMob:
            case ImperialMagicBolt.Wipe when isMob:
                _popup.PopupEntity(Loc.GetString("imperial-bolt-" + effect.ToString().ToLowerInvariant()), target, target, PopupType.Medium);
                break;
        }
    }

    /// <summary>door: стена становится открытой дверью, дверь — открывается.</summary>
    private void MagicDoor(EntityUid target)
    {
        if (TryComp<DoorComponent>(target, out var door))
        {
            if (TryComp<DoorBoltComponent>(target, out var bolts))
                _doors.SetBoltsDown((target, bolts), false);
            _doors.TryOpen(target, door);
            return;
        }

        if (!Transform(target).Anchored || !EntityManager.System<TagSystem>().HasTag(target, WallTagId))
            return;

        var coords = Transform(target).Coordinates;
        QueueDel(target);
        var newDoor = Spawn(_random.Pick(DoorTypes), coords);
        if (TryComp<DoorComponent>(newDoor, out var created))
            _doors.TryOpen(newDoor, created);
    }

    /// <summary>wabbajack(): случайное существо — обезьяна, животное или человек другой расы.</summary>
    private void Wabbajack(EntityUid target)
    {
        var config = new PolymorphConfiguration
        {
            Entity = _random.Pick(WabbajackForms),
            Forced = true,
            Inventory = PolymorphInventoryChange.Drop,
            TransferName = true,
            TransferDamage = false,
            RevertOnCrit = false,
            RevertOnDeath = false,
            AllowRepeatedMorphs = true,
        };
        if (_polymorph.PolymorphEntity(target, config) is { } result)
            _popup.PopupEntity(Loc.GetString("imperial-bolt-wabbajack"), result, result, PopupType.Large);
    }

    /// <summary>animate_atom_living(): предмет оживает и служит стрелку.</summary>
    private void AnimateObject(EntityUid target, EntityUid? owner)
    {
        // В TG стены — тайлы, оживить их нельзя.
        if (EntityManager.System<TagSystem>().HasTag(target, WallTagId))
            return;

        if (Transform(target).Anchored && HasComp<Content.Server.Power.Components.ApcPowerReceiverComponent>(target))
            return;

        if (MetaData(target).EntityPrototype is not { } proto)
            return;

        var mimic = Spawn("ImperialAnimatedObject", Transform(target).Coordinates);
        var comp = EnsureComp<ImperialAnimatedComponent>(mimic);
        comp.Proto = proto.ID;
        Dirty(mimic, comp);
        _meta.SetEntityName(mimic, Name(target));
        if (owner != null && TryComp<Content.Shared.NPC.Components.NpcFactionMemberComponent>(owner, out var ownerFactions))
        {
            foreach (var faction in ownerFactions.Factions)
            {
                _faction.AddFaction(mimic, faction);
            }
        }

        QueueDel(target);
    }

    /// <summary>locker: жертва оказывается в заваренном проклятом шкафу.</summary>
    private void CursedLocker(EntityUid target, EntityCoordinates impact)
    {
        var locker = Spawn("ImperialCursedLocker", Transform(target).Coordinates);
        EnsureComp<ImperialDecayLockerComponent>(locker).BustAt = _timing.CurTime + TimeSpan.FromMinutes(5);
        if (TryComp<EntityStorageComponent>(locker, out var storage))
        {
            _storage.Insert(target, locker, storage);
            if (TryComp<Content.Shared.Tools.Components.WeldableComponent>(locker, out var weldable))
                EntityManager.System<Content.Shared.Tools.Systems.WeldableSystem>().SetWeldedState(locker, true, weldable);
        }
    }

    private void UpdateLockers()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialDecayLockerComponent, EntityStorageComponent>();
        var bust = new List<(EntityUid, EntityStorageComponent)>();
        while (query.MoveNext(out var uid, out var locker, out var storage))
        {
            if (now >= locker.BustAt)
                bust.Add((uid, storage));
        }

        foreach (var (uid, storage) in bust)
        {
            if (TryComp<Content.Shared.Tools.Components.WeldableComponent>(uid, out var weldable))
                EntityManager.System<Content.Shared.Tools.Systems.WeldableSystem>().SetWeldedState(uid, false, weldable);
            _storage.OpenStorage(uid, storage);
            QueueDel(uid);
        }
    }

    /// <summary>shrink: цель уменьшается на время.</summary>
    public void Shrink(EntityUid target, TimeSpan duration)
    {
        EnsureComp<ImperialShrunkComponent>(target);
        EnsureComp<ImperialShrinkTimerComponent>(target).End = _timing.CurTime + duration;
        Dirty(target, Comp<ImperialShrunkComponent>(target));
    }

    private void UpdateShrinks()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialShrinkTimerComponent>();
        var done = new List<EntityUid>();
        while (query.MoveNext(out var uid, out var timer))
        {
            if (now >= timer.End)
                done.Add(uid);
        }

        foreach (var uid in done)
        {
            RemComp<ImperialShrinkTimerComponent>(uid);
            RemComp<ImperialShrunkComponent>(uid);
        }
    }

    #endregion

    private void UpdateMagicGuns()
    {
        UpdateLockers();
        UpdateShrinks();
    }
}
