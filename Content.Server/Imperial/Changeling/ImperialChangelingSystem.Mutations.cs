using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Fluids;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Changeling;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server.Imperial.Changeling;

/// <summary>
/// Мутации TG (mutations.dm): рука-клинок, щупальце, органический щит, хитиновая броня, голова-улей.
/// </summary>
public sealed partial class ImperialChangelingSystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly Content.Shared.Fluids.SharedPuddleSystem _puddle = default!;

    public const string ArmBladePower = "ImperialChangelingArmBladePower";
    public const string TentaclePower = "ImperialChangelingTentaclePower";
    public const string ShieldPower = "ImperialChangelingShieldPower";
    public const string ArmorPower = "ImperialChangelingArmorPower";
    public const string HiveHeadPower = "ImperialChangelingHiveHeadPower";

    private static readonly EntProtoId ArmBlade = "ImperialChangelingArmBlade";
    private static readonly EntProtoId Tentacle = "ImperialChangelingTentacle";
    private static readonly EntProtoId Shield = "ImperialChangelingShield";
    private static readonly EntProtoId Armor = "ImperialChangelingArmor";
    private static readonly EntProtoId Helmet = "ImperialChangelingHelmet";
    private static readonly EntProtoId HiveHead = "ImperialChangelingHiveHead";
    private static readonly EntProtoId Bee = "MobAngryBee";

    private static readonly SoundSpecifier Splat = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");
    private static readonly SoundSpecifier BeeSwarm = new SoundPathSpecifier("/Audio/Imperial/Changeling/bee_swarm.ogg");

    /// <summary>Органическое оружие: тип предмета, «простое» имя для сообщений и тихое ли превращение.</summary>
    private readonly Dictionary<string, (EntProtoId Item, string Name, bool Silent)> _weapons = new();

    /// <summary>Органический костюм: костюм, шлем, расход химикатов и кровь при сбросе.</summary>
    private readonly Dictionary<string, (EntProtoId? Suit, EntProtoId? Helmet, string Name, float Slowdown, bool Blood)> _suits = new();

    /// <summary>tentacle_grab(): кого подтащили щупальцем в боевой стойке.</summary>
    private readonly List<(EntityUid Ling, EntityUid Victim, TimeSpan At)> _pendingTentacleGrab = new();

    private void InitializeMutations()
    {
        _weapons[ArmBladePower] = (ArmBlade, "blade", false);
        _weapons[TentaclePower] = (Tentacle, "tentacle", true);
        _weapons[ShieldPower] = (Shield, "shield", false);
        _suits[ArmorPower] = (Armor, Helmet, "armor", 0.125f, false);
        _suits[HiveHeadPower] = (null, HiveHead, "hive", 0f, true);

        foreach (var (power, weapon) in _weapons)
        {
            _powers[power] = (ent, _) => SpawnWeapon(ent, power, weapon.Item, weapon.Silent);
            _powerPreUse[power] = ent => UnequipWeapon(ent, weapon.Item, weapon.Name, weapon.Silent);
            _powerRemoved[power] = ent => UnequipWeapon(ent, weapon.Item, weapon.Name, weapon.Silent);
        }

        foreach (var (power, suit) in _suits)
        {
            _powers[power] = (ent, _) => SpawnSuit(ent, suit.Suit, suit.Helmet, suit.Slowdown);
            _powerPreUse[power] = ent => CastOffSuit(ent, suit.Suit, suit.Helmet, suit.Name, suit.Slowdown, suit.Blood);
            _powerRemoved[power] = ent => CastOffSuit(ent, suit.Suit, suit.Helmet, suit.Name, suit.Slowdown, suit.Blood);
        }

        SubscribeLocalEvent<ImperialChangelingComponent, DamageModifyEvent>(OnShieldDamage);
        SubscribeLocalEvent<ImperialChangelingTentacleComponent, GunShotEvent>(OnTentacleShot);
        SubscribeLocalEvent<ImperialChangelingTentacleProjectileComponent, ProjectileHitEvent>(OnTentacleHit);
        SubscribeLocalEvent<ImperialChangelingHiveHeadComponent, ImperialChangelingReleaseBeesEvent>(OnReleaseBees);
        SubscribeLocalEvent<ImperialChangelingArmBladeComponent, BeforeRangedInteractEvent>(OnArmBladeInteract);
    }

    /// <summary>arm_blade/afterattack(): запитанный шлюз скрежещет, пока его вскрывают.</summary>
    private void OnArmBladeInteract(Entity<ImperialChangelingArmBladeComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } door || !TryComp<DoorComponent>(door, out var doorComp)
            || doorComp.State != DoorState.Closed || !TryComp<AirlockComponent>(door, out var airlock) || !airlock.Powered)
            return;

        if (TryComp<DoorBoltComponent>(door, out var bolts) && bolts.BoltsDown)
            return;

        _popup.PopupEntity(Loc.GetString("imperial-changeling-armblade-pry", ("user", Identity.Entity(args.User, EntityManager)), ("door", door)),
            args.User, PopupType.MediumCaution);
        _audio.PlayPvs(ent.Comp.PrySound, door);
    }

    private void UpdateMutations(TimeSpan now)
    {
        for (var i = _pendingTentacleGrab.Count - 1; i >= 0; i--)
        {
            var (ling, victim, at) = _pendingTentacleGrab[i];
            if (now < at)
                continue;

            _pendingTentacleGrab.RemoveAt(i);
            TentacleGrab(ling, victim);
        }
    }

    #region Оружие

    private bool IsHeld(EntityUid item, EntProtoId proto)
    {
        return MetaData(item).EntityPrototype?.ID == proto.Id;
    }

    /// <summary>unequip_held() → check_weapon(): втянуть оружие обратно.</summary>
    private bool UnequipWeapon(Entity<ImperialChangelingComponent> ent, EntProtoId weapon, string name, bool silent)
    {
        var found = false;
        foreach (var held in _hands.EnumerateHeld(ent.Owner))
        {
            if (!IsHeld(held, weapon))
                continue;

            found = true;
            Del(held);
            if (silent)
                continue;

            _audio.PlayPvs(BlobAttack, ent);
            _popup.PopupEntity(Loc.GetString($"imperial-changeling-weapon-{name}-retract-others", ("user", Identity.Entity(ent, EntityManager))),
                ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);
            Popup(ent, $"imperial-changeling-weapon-{name}-retract-self");
        }

        return found;
    }

    /// <summary>weapon/sting_action(): оружие в активную руку.</summary>
    private bool SpawnWeapon(Entity<ImperialChangelingComponent> ent, string power, EntProtoId weapon, bool silent)
    {
        // regenerate_limb(): без рук оружию негде вырасти — сначала отращиваем правую.
        if (_hands.GetActiveHand(ent.Owner) == null && RegenerateLimbs(ent, new[] { "ArmRight", "HandRight" }))
            LimbsRegrownFeedback(ent, "imperial-changeling-arm-regrow-others", "imperial-changeling-arm-regrow-self", sound: false);

        if (_hands.GetActiveHand(ent.Owner) is not { } hand)
        {
            Popup(ent, "imperial-changeling-weapon-wrong-form");
            return false;
        }

        if (_hands.GetActiveItem(ent.Owner) is { } held && !_hands.TryDrop(ent.Owner, held))
        {
            Popup(ent, "imperial-changeling-weapon-hand-busy");
            return false;
        }

        var item = Spawn(weapon, Transform(ent).Coordinates);
        if (!_hands.TryPickup(ent.Owner, item, hand))
        {
            QueueDel(item);
            return false;
        }

        // Щит: remaining_uses = absorbed_count * 3.
        if (TryComp<ImperialChangelingShieldComponent>(item, out var shield))
            shield.RemainingUses = ent.Comp.AbsorbedCount * 3;

        var name = _weapons[power].Name;
        if (silent)
        {
            Popup(ent, $"imperial-changeling-weapon-{name}-form-self");
            return true;
        }

        _audio.PlayPvs(BlobAttack, ent);
        _popup.PopupEntity(Loc.GetString($"imperial-changeling-weapon-{name}-form-others", ("user", Identity.Entity(ent, EntityManager))),
            ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);
        Popup(ent, $"imperial-changeling-weapon-{name}-form-self");
        return true;
    }

    /// <summary>shield/hit_reaction(): 50% блок, пока не кончились удары.</summary>
    private void OnShieldDamage(EntityUid uid, ImperialChangelingComponent comp, DamageModifyEvent args)
    {
        if (args.Origin is not { } origin || origin == uid || !args.Damage.AnyPositive())
            return;

        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (!TryComp<ImperialChangelingShieldComponent>(held, out var shield))
                continue;

            if (shield.RemainingUses < 1)
            {
                _popup.PopupEntity(Loc.GetString("imperial-changeling-weapon-shield-break-others", ("user", Identity.Entity(uid, EntityManager))),
                    uid, Filter.PvsExcept(uid), true, PopupType.MediumCaution);
                Popup(uid, "imperial-changeling-weapon-shield-break-self");
                QueueDel(held);
                return;
            }

            shield.RemainingUses--;
            if (!_random.Prob(shield.BlockChance))
                return;

            _popup.PopupEntity(Loc.GetString("imperial-changeling-weapon-shield-block", ("user", Identity.Entity(uid, EntityManager))), uid, PopupType.Small);
            args.Damage = new DamageSpecifier();
            return;
        }
    }

    #endregion

    #region Щупальце

    /// <summary>Щупальце одноразовое (max_charges = 1): после выстрела рука возвращается.</summary>
    private void OnTentacleShot(Entity<ImperialChangelingTentacleComponent> ent, ref GunShotEvent args)
    {
        foreach (var (shot, _) in args.Ammo)
        {
            if (shot is not { } projectile)
                continue;

            var visuals = EnsureComp<Content.Shared.Physics.JointVisualsComponent>(projectile);
            visuals.Sprite = new Robust.Shared.Utility.SpriteSpecifier.Rsi(new Robust.Shared.Utility.ResPath("/Textures/Imperial/Changeling/tentacle_beam.rsi"), "tentacle");
            visuals.Target = args.User;
            Dirty(projectile, visuals);
        }

        QueueDel(ent);
    }

    /// <summary>tentacle/on_hit().</summary>
    private void OnTentacleHit(Entity<ImperialChangelingTentacleProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (args.Shooter is not { } ling || !TryComp<ImperialChangelingComponent>(ling, out _))
            return;

        var target = args.Target;
        var lingCoords = Transform(ling).Coordinates;
        if (HasComp<ItemComponent>(target) && !HasComp<MobStateComponent>(target))
        {
            if (Transform(target).Anchored)
                return;

            Popup(ling, "imperial-changeling-tentacle-item", ("item", target));
            _throwing.TryThrow(target, lingCoords, 6f, ling, doSpin: false);
            return;
        }

        if (!HasComp<MobStateComponent>(target) || Transform(target).Anchored)
            return;

        // Шаг перед генокрадом.
        var direction = (_transform.GetWorldPosition(target) - _transform.GetWorldPosition(ling));
        var step = direction.LengthSquared() > 0.01f ? direction.Normalized() : System.Numerics.Vector2.Zero;
        var land = lingCoords.Offset(step);

        if (!HasComp<Content.Shared.Humanoid.HumanoidProfileComponent>(target) || !_combat.IsInCombatMode(ling))
        {
            _popup.PopupEntity(Loc.GetString("imperial-changeling-tentacle-pull-others", ("target", Identity.Entity(target, EntityManager)), ("user", Identity.Entity(ling, EntityManager))),
                target, Filter.PvsExcept(target), true, PopupType.MediumCaution);
            _popup.PopupEntity(Loc.GetString("imperial-changeling-tentacle-pull-target", ("user", Identity.Entity(ling, EntityManager))), target, target, PopupType.LargeCaution);
            _throwing.TryThrow(target, land, 6f, ling, doSpin: false);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-changeling-tentacle-throw-others", ("target", Identity.Entity(target, EntityManager)), ("user", Identity.Entity(ling, EntityManager))),
            target, Filter.PvsExcept(target), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("imperial-changeling-tentacle-throw-target", ("user", Identity.Entity(ling, EntityManager))), target, target, PopupType.LargeCaution);
        _throwing.TryThrow(target, land, 6f, ling, doSpin: false);
        _pendingTentacleGrab.Add((ling, target, _timing.CurTime + TimeSpan.FromSeconds(0.6)));
    }

    /// <summary>tentacle_grab(): захват и удар острым оружием из рук.</summary>
    private void TentacleGrab(EntityUid ling, EntityUid victim)
    {
        if (TerminatingOrDeleted(ling) || TerminatingOrDeleted(victim) || !_interaction.InRangeUnobstructed(ling, victim, 1.5f))
            return;

        _pulling.TryStartPull(ling, victim);
        foreach (var held in _hands.EnumerateHeld(ling))
        {
            if (!HasComp<Content.Shared.Kitchen.Components.SharpComponent>(held) || !TryComp<MeleeWeaponComponent>(held, out var melee))
                continue;

            _popup.PopupEntity(Loc.GetString("imperial-changeling-tentacle-stab", ("user", Identity.Entity(ling, EntityManager)), ("target", Identity.Entity(victim, EntityManager)), ("weapon", held)),
                victim, PopupType.LargeCaution);
            _damage.TryChangeDamage(victim, melee.Damage, origin: ling);
            if (melee.HitSound != null)
                _audio.PlayPvs(melee.HitSound, victim);
            return;
        }
    }

    #endregion

    #region Костюмы

    /// <summary>check_suit(): сбросить хитин или голову-улей.</summary>
    private bool CastOffSuit(Entity<ImperialChangelingComponent> ent, EntProtoId? suit, EntProtoId? helmet, string name, float slowdown, bool blood)
    {
        var user = ent.Owner;
        var wearing = suit is { } s && _inventory.TryGetSlotEntity(user, "outerClothing", out var outer) && IsHeld(outer.Value, s)
            || helmet is { } h && _inventory.TryGetSlotEntity(user, "head", out var head) && IsHeld(head.Value, h);
        if (!wearing)
            return false;

        _popup.PopupEntity(Loc.GetString($"imperial-changeling-suit-{name}-castoff-others", ("user", Identity.Entity(user, EntityManager))),
            user, Filter.PvsExcept(user), true, PopupType.MediumCaution);
        Popup(user, $"imperial-changeling-suit-{name}-castoff-self");

        if (helmet != null && _inventory.TryGetSlotEntity(user, "head", out var headItem) && IsHeld(headItem.Value, helmet.Value))
            QueueDel(headItem.Value);
        if (suit != null && _inventory.TryGetSlotEntity(user, "outerClothing", out var suitItem) && IsHeld(suitItem.Value, suit.Value))
            QueueDel(suitItem.Value);

        if (blood)
        {
            // add_splatter_floor()
            _puddle.TrySpillAt(user, new Content.Shared.Chemistry.Components.Solution("Blood", 5), out _, sound: false);
            _audio.PlayPvs(Splat, user);
        }

        ent.Comp.RechargeSlowdown -= slowdown;
        return true;
    }

    /// <summary>suit/sting_action().</summary>
    private bool SpawnSuit(Entity<ImperialChangelingComponent> ent, EntProtoId? suit, EntProtoId? helmet, float slowdown)
    {
        var user = ent.Owner;
        if (suit != null && _inventory.TryGetSlotEntity(user, "outerClothing", out var outer) && HasComp<Content.Shared.Interaction.Components.UnremoveableComponent>(outer))
        {
            Popup(user, "imperial-changeling-suit-body-busy");
            return false;
        }

        if (helmet != null && _inventory.TryGetSlotEntity(user, "head", out var head) && HasComp<Content.Shared.Interaction.Components.UnremoveableComponent>(head))
        {
            Popup(user, "imperial-changeling-suit-head-busy");
            return false;
        }

        if (suit is { } suitProto)
            EquipFlesh(user, suitProto, "outerClothing");
        if (helmet is { } helmetProto)
            EquipFlesh(user, helmetProto, "head");

        if (suit != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-changeling-suit-armor-form-others", ("user", Identity.Entity(user, EntityManager))),
                user, Filter.PvsExcept(user), true, PopupType.MediumCaution);
            Popup(user, "imperial-changeling-suit-armor-form-self");
        }

        ent.Comp.RechargeSlowdown += slowdown;
        return true;
    }

    private void EquipFlesh(EntityUid user, EntProtoId proto, string slot)
    {
        if (_inventory.TryGetSlotEntity(user, slot, out _))
            _inventory.TryUnequip(user, slot, force: true);

        var item = Spawn(proto, Transform(user).Coordinates);
        if (!_inventory.TryEquip(user, item, slot, silent: true, force: true))
            QueueDel(item);
    }

    /// <summary>hivehead_spawn_minions: пчёлы атакуют всех, кроме носителя.</summary>
    private void OnReleaseBees(Entity<ImperialChangelingHiveHeadComponent> ent, ref ImperialChangelingReleaseBeesEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var owner = args.Performer;
        _popup.PopupEntity(Loc.GetString("imperial-changeling-hive-release-others", ("user", Identity.Entity(owner, EntityManager))),
            owner, Filter.PvsExcept(owner), true, PopupType.MediumCaution);
        Popup(owner, "imperial-changeling-hive-release-self");
        _audio.PlayPvs(BeeSwarm, owner);

        var count = _mobState.IsCritical(owner) ? 1 : ent.Comp.SpawnCount;
        for (var i = 0; i < count; i++)
        {
            var bee = Spawn(Bee, Transform(owner).Coordinates);
            var exception = EnsureComp<FactionExceptionComponent>(bee);
            _faction.IgnoreEntity((bee, exception), owner);
            AssignBeeReagent(bee, ent.Owner);
            EnsureComp<TimedDespawnComponent>(bee).Lifetime = (float) ent.Comp.Lifespan.TotalSeconds;
        }
    }

    #endregion
}
