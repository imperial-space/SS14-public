using System.Linq;
using System.Numerics;
using Content.Shared.Coordinates.Helpers;
using Content.Server.Imperial.Cult.Components;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// Конструкты культа (basic/cult/constructs), тени, камень душ и оболочка (soulstone.dm).
/// </summary>
public sealed partial class CultConstructSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CultSystem _cult = default!;
    [Dependency] private readonly CultRuneSystem _runes = default!;
    [Dependency] private readonly CultStructureSystem _structures = default!;
    [Dependency] private readonly ImperialCultRuleSystem _rule = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    private const string ShadeContainer = "soulstone_shade";

    public static readonly Dictionary<CultConstructType, EntProtoId> ConstructProtos = new()
    {
        { CultConstructType.Juggernaut, "MobCultJuggernaut" },
        { CultConstructType.Wraith, "MobCultWraith" },
        { CultConstructType.Artificer, "MobCultArtificer" },
        { CultConstructType.Harvester, "MobCultHarvester" },
        { CultConstructType.Proteon, "MobCultProteon" },
    };

    private static readonly Dictionary<CultConstructType, string> ConstructStates = new()
    {
        { CultConstructType.Juggernaut, "juggernaut" },
        { CultConstructType.Wraith, "wraith" },
        { CultConstructType.Artificer, "artificer" },
        { CultConstructType.Harvester, "harvester" },
        { CultConstructType.Proteon, "proteon" },
    };

    public override void Initialize()
    {
        base.Initialize();

        InitializeHarvester();
        SubscribeLocalEvent<CultConstructComponent, MapInitEvent>(OnConstructInit);
        SubscribeLocalEvent<CultConstructComponent, MindAddedMessage>(OnConstructMind);
        SubscribeLocalEvent<CultConstructComponent, ExaminedEvent>(OnConstructExamined);
        SubscribeLocalEvent<CultConstructComponent, MobStateChangedEvent>(OnConstructState);
        SubscribeLocalEvent<CultConstructComponent, MeleeHitEvent>(OnConstructMelee);
        SubscribeLocalEvent<CultConstructComponent, UserActivateInWorldEvent>(OnConstructActivate);

        SubscribeLocalEvent<CultConstructComponent, CultGauntletEchoActionEvent>(OnGauntletEcho);
        SubscribeLocalEvent<CultConstructComponent, CultForcewallActionEvent>(OnForcewall);
        SubscribeLocalEvent<CultConstructComponent, CultPhaseShiftActionEvent>(OnPhaseShift);
        SubscribeLocalEvent<CultConstructComponent, CultMagicMissileActionEvent>(OnMagicMissile);
        SubscribeLocalEvent<CultConstructComponent, CultConjureActionEvent>(OnConjure);
        SubscribeLocalEvent<CultConstructComponent, CultCreateRuneActionEvent>(OnCreateRune);
        SubscribeLocalEvent<CultConstructComponent, CultCreateRuneDoAfterEvent>(OnCreateRuneDoAfter);
        SubscribeLocalEvent<CultConstructComponent, CultAreaConversionActionEvent>(OnAreaConversion);
        SubscribeLocalEvent<CultConstructComponent, CultConstructSpawnItemActionEvent>(OnLegacySpawnItem);
        SubscribeLocalEvent<CultConstructComponent, CultConstructSpawnStructureActionEvent>(OnLegacySpawnStructure);
        SubscribeLocalEvent<CultConstructComponent, CultConstructCreateFloorActionEvent>(OnLegacyFloor);

        SubscribeLocalEvent<CultShadeComponent, MapInitEvent>(OnShadeInit);
        SubscribeLocalEvent<CultShadeComponent, MobStateChangedEvent>(OnShadeState);

        SubscribeLocalEvent<CultPhasedComponent, InteractionAttemptEvent>(OnPhasedInteract);
        SubscribeLocalEvent<CultPhasedComponent, AttackAttemptEvent>(OnPhasedAttack);

        SubscribeLocalEvent<CultSoulstoneComponent, MapInitEvent>(OnStoneInit);
        SubscribeLocalEvent<CultSoulstoneComponent, AfterInteractEvent>(OnStoneInteract);
        SubscribeLocalEvent<CultSoulstoneComponent, UseInHandEvent>(OnStoneUse);
        SubscribeLocalEvent<CultSoulstoneComponent, GettingPickedUpAttemptEvent>(OnStonePickup);
        SubscribeLocalEvent<CultSoulstoneComponent, ExaminedEvent>(OnStoneExamined);
        SubscribeLocalEvent<CultSoulstoneComponent, EntityTerminatingEvent>(OnStoneTerminating);

        SubscribeLocalEvent<CultConstructShellComponent, InteractUsingEvent>(OnShellInteractUsing);
        SubscribeLocalEvent<CultConstructShellComponent, ExaminedEvent>(OnShellExamined);

        SubscribeLocalEvent<CultBloodBoltComponent, ProjectileHitEvent>(OnBoltHit);
        SubscribeLocalEvent<CultGauntletProjectileComponent, ProjectileHitEvent>(OnGauntletHit);
        SubscribeLocalEvent<CultMissileProjectileComponent, ProjectileHitEvent>(OnMissileHit);
    }

    #region Конструкты

    private void OnConstructInit(Entity<CultConstructComponent> ent, ref MapInitEvent args)
    {
        foreach (var proto in ent.Comp.Actions)
        {
            EntityUid? action = null;
            if (_cultActions().AddAction(ent, ref action, proto) && action != null)
                ent.Comp.GrantedActions.Add(action.Value);
        }
        _cult.AddConstructFaction(ent);
    }

    private Content.Shared.Actions.SharedActionsSystem _cultActions() => EntityManager.System<Content.Shared.Actions.SharedActionsSystem>();

    private void OnConstructMind(Entity<CultConstructComponent> ent, ref MindAddedMessage args)
    {
        _cult.Message(ent, Loc.GetString(ent.Comp.PlaystyleString));
    }

    private void OnConstructExamined(Entity<CultConstructComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp<DamageableComponent>(ent, out var damageable) || _cult.TotalDamage(ent) <= 0)
            return;
        var health = _cult.HealthOf(ent, damageable);
        var max = health + _cult.TotalDamage(ent);
        args.PushMarkup(Loc.GetString(health >= max / 2 ? "cult-construct-dented" : "cult-construct-severely-dented", ("target", ent)));
    }

    private void OnConstructState(Entity<CultConstructComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;
        // DEL_ON_DEATH + death_message + death_drops (эктоплазма).
        _popup.PopupEntity(Loc.GetString("cult-construct-death", ("target", ent)), ent, PopupType.MediumCaution);
        Spawn("CultEctoplasm", Transform(ent).Coordinates);
        QueueDel(ent);
    }

    /// <summary>Атаки призрака снижают перезарядку фазового сдвига (recharging_attacks).</summary>
    private void OnConstructMelee(Entity<CultConstructComponent> ent, ref MeleeHitEvent args)
    {
        if (ent.Comp.ConstructType == CultConstructType.Harvester && args.HitEntities.Count > 0)
        {
            foreach (var hit in args.HitEntities)
            {
                if (TryHarvest(ent, hit))
                {
                    args.Handled = true;
                    return;
                }
            }
        }

        if (ent.Comp.ConstructType != CultConstructType.Wraith || args.HitEntities.Count == 0)
            return;
        var fatal = args.HitEntities.Any(e => _mobState.IsDead(e));
        foreach (var action in ent.Comp.GrantedActions)
        {
            if (!TryComp<Content.Shared.Actions.Components.ActionComponent>(action, out var comp) || comp.Cooldown is not { } cd)
                continue;
            if (MetaData(action).EntityPrototype?.ID != "ActionCultPhaseShift")
                continue;
            var reduce = TimeSpan.FromSeconds(fatal ? 5 : 1);
            var end = cd.End - reduce;
            _cultActions().SetCooldown(action, cd.Start, end < _timing.CurTime ? _timing.CurTime : end);
        }
    }

    /// <summary>healing_touch / structure_repair ремесленника: 5 brute.</summary>
    private void OnConstructActivate(Entity<CultConstructComponent> ent, ref UserActivateInWorldEvent args)
    {
        if (args.Handled || !ent.Comp.CanRepair)
            return;
        var target = args.Target;
        if (target == ent.Owner && !ent.Comp.CanRepairSelf)
            return;

        var repairable = HasComp<CultConstructComponent>(target) || HasComp<CultShadeComponent>(target) || HasComp<CultStructureComponent>(target);
        if (!repairable || !TryComp<DamageableComponent>(target, out var damageable))
            return;
        args.Handled = true;

        if (_cult.TotalDamage(target) <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-construct-repair-full", ("target", target)), ent, ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cult-construct-repair-start", ("user", ent), ("target", target)), target, PopupType.Small);
        _cult.HealGroup(target, "Brute", 5f);
        _popup.PopupEntity(Loc.GetString("cult-construct-repair-done", ("target", target)), target, PopupType.Small);
        _cult.Effect("CultEffectHeal", Transform(target).Coordinates);
    }

    public bool BorgHasMind(EntityUid borg)
    {
        if (_mind.TryGetMind(borg, out _, out _))
            return true;
        return TryComp<BorgChassisComponent>(borg, out var chassis) && chassis.BrainEntity != null;
    }

    /// <summary>GLOB.construct_radial_images.</summary>
    public void OpenConstructChoice(EntityUid user, Action<CultConstructType> callback)
    {
        var rsi = new ResPath("/Textures/Imperial/BloodCult/constructs.rsi");
        var options = new List<CultMenuOption>
        {
            new(nameof(CultConstructType.Juggernaut), Loc.GetString("cult-construct-name-juggernaut"), new SpriteSpecifier.Rsi(rsi, "juggernaut"), Loc.GetString("cult-construct-tip-juggernaut")),
            new(nameof(CultConstructType.Wraith), Loc.GetString("cult-construct-name-wraith"), new SpriteSpecifier.Rsi(rsi, "wraith"), Loc.GetString("cult-construct-tip-wraith")),
            new(nameof(CultConstructType.Artificer), Loc.GetString("cult-construct-name-artificer"), new SpriteSpecifier.Rsi(rsi, "artificer"), Loc.GetString("cult-construct-tip-artificer")),
        };
        _cult.OpenChoice(user, Loc.GetString("cult-construct-choice-title"), options, id =>
        {
            if (Enum.TryParse<CultConstructType>(id, out var type))
                callback(type);
        });
    }

    /// <summary>make_new_construct().</summary>
    public EntityUid? MakeConstruct(CultConstructType type, EntityUid target, EntityUid? creator, EntityCoordinates coords, EntityUid? fromMind = null, bool stoneCaptured = true)
    {
        if (!ConstructProtos.TryGetValue(type, out var proto))
            return null;

        var construct = Spawn(proto, coords);
        if (ConstructStates.TryGetValue(type, out var state))
            _cult.Effect("CultEffectConstructForm", coords, color: null).Also(e => _appearance.SetData(e, CultVisuals.State, $"make_{state}_cult"));
        _audio.PlayPvs(CultSounds.ConstructForm, construct, AudioParams.Default.WithVolume(CultSystem.Db(50)));

        if (TryComp<CultConstructComponent>(construct, out var comp))
            comp.Master = creator;

        var mindSource = fromMind ?? target;
        if (_mind.TryGetMind(mindSource, out var mindId, out var mind))
        {
            _mind.TransferTo(mindId, construct, mind: mind);
        }
        else if (creator != null)
        {
            _cult.Message(creator.Value, Loc.GetString("cult-construct-imbue-failed"));
            _cult.MakeGhostRole(construct, Loc.GetString($"cult-construct-name-{type.ToString().ToLowerInvariant()}"));
        }

        if (creator != null && _cult.IsCultist(creator.Value) || _cult.IsCultAligned(target))
            _cult.AddCultist(construct, silent: true);
        if (comp != null && creator != null)
            GrantSeekMaster(construct, comp);
        if (type == CultConstructType.Harvester)
            StartHarvesterSeek(construct);
        return construct;
    }

    #endregion

    #region Заклинания конструктов

    private void OnGauntletEcho(Entity<CultConstructComponent> ent, ref CultGauntletEchoActionEvent args)
    {
        args.Handled = true;
        var from = _transform.GetMapCoordinates(ent);
        var to = _transform.ToMapCoordinates(args.Target);
        var dir = to.Position - from.Position;
        if (dir.LengthSquared() < 0.01f)
            return;
        _audio.PlayPvs(CultSounds.ResonatorBlast, ent);
        var proj = Spawn("CultGauntletEcho", from);
        _gun.ShootProjectile(proj, Vector2.Normalize(dir), Vector2.Zero, ent, ent, 3f);
    }

    private void OnGauntletHit(Entity<CultGauntletProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        var coords = Transform(ent).Coordinates;
        _audio.PlayPvs(CultSounds.ResonatorBlast, coords);
        _cult.Effect("CultEffectSacrifice", coords);
        if (HasComp<MobStateComponent>(args.Target) && !_cult.IsCultAligned(args.Target))
            EntityManager.System<Content.Shared.Stunnable.SharedStunSystem>().TryKnockdown(args.Target, TimeSpan.FromSeconds(5), true);

        foreach (var obj in _lookup.GetEntitiesInRange(coords, 1.5f, LookupFlags.Static | LookupFlags.Dynamic))
        {
            if (HasComp<MobStateComponent>(obj) || HasComp<CultStructureComponent>(obj))
                continue;
            if (!TryComp<PhysicsComponent>(obj, out var physics) || !physics.Hard || !physics.CanCollide || !HasComp<DamageableComponent>(obj))
                continue;
            _cult.Damage(obj, "Blunt", 90, ignoreResistances: false);
            _cult.Effect("CultEffectFloorGlow", Transform(obj).Coordinates);
        }
    }

    /// <summary>forcewall/cult: три стены перед собой на 20 с.</summary>
    private void OnForcewall(Entity<CultConstructComponent> ent, ref CultForcewallActionEvent args)
    {
        args.Handled = true;
        _audio.PlayPvs(CultSounds.Forcewall, ent);
        var xform = Transform(ent);
        var facing = xform.LocalRotation.GetCardinalDir();
        var left = facing.ToAngle() + Angle.FromDegrees(90);
        var center = xform.Coordinates.SnapToGrid(EntityManager);
        var wall = Spawn("CultWallForce", center);
        EnsureComp<PreventCollideComponent>(wall).Uid = ent;
        Dirty(wall, Comp<PreventCollideComponent>(wall));
        Spawn("CultWallForce", center.Offset(left.ToVec()));
        Spawn("CultWallForce", center.Offset(-left.ToVec()));
    }

    /// <summary>ethereal_jaunt/shift: 5 с сквозь стены.</summary>
    private void OnPhaseShift(Entity<CultConstructComponent> ent, ref CultPhaseShiftActionEvent args)
    {
        if (HasComp<CultPhasedComponent>(ent))
            return;
        args.Handled = true;
        _audio.PlayPvs(CultSounds.EtherealEnter, ent, AudioParams.Default.WithVolume(CultSystem.Db(50)));
        _cult.Effect("CultEffectWraithOut", Transform(ent).Coordinates, Transform(ent).LocalRotation);

        var phased = EnsureComp<CultPhasedComponent>(ent);
        phased.End = _timing.CurTime + TimeSpan.FromSeconds(5);
        SetPhased(ent, phased, true);
    }

    private void SetPhased(EntityUid uid, CultPhasedComponent phased, bool enabled)
    {
        if (TryComp<FixturesComponent>(uid, out var fixtures))
        {
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                if (enabled)
                {
                    phased.Masks[id] = (fixture.CollisionMask, fixture.CollisionLayer);
                    _physics.SetCollisionMask(uid, id, fixture, 0, fixtures);
                    _physics.SetCollisionLayer(uid, id, fixture, 0, fixtures);
                }
                else if (phased.Masks.TryGetValue(id, out var old))
                {
                    _physics.SetCollisionMask(uid, id, fixture, old.Mask, fixtures);
                    _physics.SetCollisionLayer(uid, id, fixture, old.Layer, fixtures);
                }
            }
        }
        var vis = EnsureComp<VisibilityComponent>(uid);
        _visibility.SetLayer((uid, vis), (ushort) (enabled ? VisibilityFlags.Ghost : VisibilityFlags.Normal));
    }

    private void OnPhasedInteract(Entity<CultPhasedComponent> ent, ref InteractionAttemptEvent args) => args.Cancelled = true;
    private void OnPhasedAttack(Entity<CultPhasedComponent> ent, ref AttackAttemptEvent args) => args.Cancel();

    /// <summary>magic_missile/lesser: до 6 целей в поле зрения.</summary>
    private void OnMagicMissile(Entity<CultConstructComponent> ent, ref CultMagicMissileActionEvent args)
    {
        args.Handled = true;
        _audio.PlayPvs(CultSounds.MagicMissile, ent);
        var origin = _transform.GetMapCoordinates(ent);
        var targets = _lookup.GetEntitiesInRange<MobStateComponent>(origin, 7f)
            .Where(m => m.Owner != ent.Owner && !_cult.IsCultAligned(m) && !_mobState.IsDead(m)
                        && _examine.InRangeUnOccluded(origin, _transform.GetMapCoordinates(m), 7f, null))
            .Select(m => m.Owner).ToList();
        _random.Shuffle(targets);

        foreach (var target in targets.Take(6))
        {
            var dir = _transform.GetWorldPosition(target) - origin.Position;
            if (dir.LengthSquared() < 0.01f)
                continue;
            var proj = Spawn("CultMagicMissile", origin);
            EnsureComp<CultMissileProjectileComponent>(proj).Target = target;
            _gun.ShootProjectile(proj, Vector2.Normalize(dir), Vector2.Zero, ent, ent, 4f);
        }
    }

    private void OnMissileHit(Entity<CultMissileProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Target) || _cult.BlocksMagic(args.Target))
            return;
        _audio.PlayPvs(CultSounds.MissileHit, args.Target);
        _cult.Paralyze(args.Target, 6);
    }

    /// <summary>conjure/*: оболочка, пол культа, стена культа, камень душ.</summary>
    private void OnConjure(Entity<CultConstructComponent> ent, ref CultConjureActionEvent args)
    {
        args.Handled = true;
        var coords = Transform(ent).Coordinates;
        if (args.Floor)
        {
            _structures.MakeCultFloor(coords);
            return;
        }
        if (args.Wall)
        {
            if (_structures.IsBlocked(coords))
                return;
            Spawn("WallCultArtificer", coords.SnapToGrid(EntityManager));
            return;
        }
        if (args.Prototype is { } proto)
        {
            _audio.PlayPvs(CultSounds.SummonItems, coords);
            Spawn(proto, coords.SnapToGrid(EntityManager));
        }
    }

    /// <summary>create_rune: 6 с черчения (вдвое быстрее на полу культа).</summary>
    private void OnCreateRune(Entity<CultConstructComponent> ent, ref CultCreateRuneActionEvent args)
    {
        var coords = Transform(ent).Coordinates;
        if (!CheckRuneTurf(ent, coords))
            return;
        args.Handled = true;

        var color = Color.Red;
        if (EntityManager.ComponentFactory.TryGetRegistration(typeof(CultRuneComponent), out _)
            && IoCManager.Resolve<IPrototypeManager>().TryIndex(args.Rune, out var proto)
            && proto.TryGetComponent<CultRuneComponent>(out var runeComp, EntityManager.ComponentFactory))
            color = runeComp.RuneColor;

        var time = 6f;
        if (_structures.IsCultFloor(coords))
            time *= 0.5f;

        var effects = _runes.SpawnRuneSpawnSet(coords.SnapToGrid(EntityManager), args.Pattern, time, color);
        _audio.PlayPvs(CultSounds.EnterBlood, coords);
        var doAfter = new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(time), new CultCreateRuneDoAfterEvent { Rune = args.Rune }, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };
        if (!_doAfter.TryStartDoAfter(doAfter, out var id))
        {
            foreach (var e in effects)
                QueueDel(e);
            return;
        }
        ent.Comp.RuneEffects = effects;
    }

    private bool CheckRuneTurf(EntityUid user, EntityCoordinates coords)
    {
        var tile = EntityManager.System<Content.Shared.Maps.TurfSystem>().GetTileRef(coords);
        if (tile == null || EntityManager.System<Content.Shared.Maps.TurfSystem>().IsSpace(tile.Value))
        {
            _cult.Message(user, Loc.GetString("cult-scribe-space"));
            return false;
        }
        if (_runes.RuneAt(coords) != null)
        {
            _cult.Message(user, Loc.GetString("cult-scribe-rune-exists"));
            return false;
        }
        if (!_runes.VeilWeak(user))
        {
            _cult.Message(user, Loc.GetString("cult-scribe-veil"));
            return false;
        }
        return true;
    }

    private void OnCreateRuneDoAfter(Entity<CultConstructComponent> ent, ref CultCreateRuneDoAfterEvent args)
    {
        var effects = ent.Comp.RuneEffects;
        ent.Comp.RuneEffects = new();
        if (args.Handled)
            return;
        args.Handled = true;

        if (args.Cancelled || !CheckRuneTurf(ent, Transform(ent).Coordinates))
        {
            foreach (var e in effects)
            {
                if (!TerminatingOrDeleted(e))
                    QueueDel(e);
            }
            // Перезарядка сбрасывается при прерывании.
            if (args.Cancelled)
                ResetCreateRuneCooldown(ent, args.Rune);
            return;
        }

        var rune = _runes.CreateRune(args.Rune, Transform(ent).Coordinates, null);
        if (TryComp<CultRuneComponent>(rune, out var comp) && comp.RuneType == CultRuneType.Barrier)
            _runes.ToggleBarrierOf(rune);
    }

    private void ResetCreateRuneCooldown(Entity<CultConstructComponent> ent, string rune)
    {
        foreach (var action in ent.Comp.GrantedActions)
        {
            if (!TryComp<Content.Shared.Actions.Components.InstantActionComponent>(action, out var comp))
                continue;
            if (comp.Event is CultCreateRuneActionEvent ev && ev.Rune == rune)
                _cultActions().ClearCooldown(action);
        }
    }

    /// <summary>area_conversion: 5x5 тайлов в пол культа.</summary>
    private void OnAreaConversion(Entity<CultConstructComponent> ent, ref CultAreaConversionActionEvent args)
    {
        args.Handled = true;
        var coords = Transform(ent).Coordinates;
        for (var x = -2; x <= 2; x++)
        {
            for (var y = -2; y <= 2; y++)
            {
                var c = coords.Offset(new Vector2(x, y));
                var dist = Math.Max(Math.Abs(x), Math.Abs(y));
                if (_random.Prob(Math.Clamp((100 - dist * 25) / 100f, 0f, 1f)))
                    _structures.MakeCultFloor(c);
            }
        }
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/welder.ogg"), coords, AudioParams.Default.WithVolume(CultSystem.Db(75)));
    }

    private void OnLegacySpawnItem(Entity<CultConstructComponent> ent, ref CultConstructSpawnItemActionEvent args)
    {
        args.Handled = true;
        Spawn(args.Prototype, Transform(ent).Coordinates);
    }

    private void OnLegacySpawnStructure(Entity<CultConstructComponent> ent, ref CultConstructSpawnStructureActionEvent args)
    {
        args.Handled = true;
        Spawn(args.Prototype, args.Target.SnapToGrid(EntityManager));
    }

    private void OnLegacyFloor(Entity<CultConstructComponent> ent, ref CultConstructCreateFloorActionEvent args)
    {
        args.Handled = true;
        _structures.MakeCultFloor(args.Target);
    }

    #endregion

    #region Тени

    private void OnShadeInit(Entity<CultShadeComponent> ent, ref MapInitEvent args)
    {
        _cult.AddConstructFaction(ent);
    }

    private void OnShadeState(Entity<CultShadeComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;
        _popup.PopupEntity(Loc.GetString("cult-shade-death", ("target", ent)), ent, PopupType.MediumCaution);
        Spawn("CultEctoplasm", Transform(ent).Coordinates);
        QueueDel(ent);
    }

    #endregion

    #region Камень душ

    private void OnStoneInit(Entity<CultSoulstoneComponent> ent, ref MapInitEvent args)
    {
        _container.EnsureContainer<ContainerSlot>(ent, ShadeContainer);
        UpdateStone(ent);
    }

    private ContainerSlot Slot(EntityUid stone) => _container.EnsureContainer<ContainerSlot>(stone, ShadeContainer);

    public EntityUid? StoneShade(EntityUid stone) => Slot(stone).ContainedEntity;

    private void UpdateStone(Entity<CultSoulstoneComponent> ent)
    {
        var full = StoneShade(ent) != null;
        const string state = "soulstone";
        _appearance.SetData(ent, CultVisuals.State, full || ent.Comp.Spent ? state + "2" : state);
    }

    private bool RoleCheck(Entity<CultSoulstoneComponent> ent, EntityUid user)
    {
        return ent.Comp.Purified ? !_cult.IsCultist(user) : _cult.IsCultist(user);
    }

    private void Wracked(EntityUid user)
    {
        _cult.Unconscious(user, 10);
        _cult.Message(user, Loc.GetString("cult-soulstone-wracked"));
    }

    private void OnStonePickup(Entity<CultSoulstoneComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (!RoleCheck(ent, args.User))
            _cult.Message(args.User, Loc.GetString("cult-soulstone-dread-pickup", ("stone", ent)));
    }

    private void OnStoneExamined(Entity<CultSoulstoneComponent> ent, ref ExaminedEvent args)
    {
        if (!RoleCheck(ent, args.Examiner) && !HasComp<Content.Shared.Ghost.GhostComponent>(args.Examiner))
            return;
        args.PushMarkup(Loc.GetString("cult-soulstone-examine-1"));
        args.PushMarkup(Loc.GetString("cult-soulstone-examine-2"));
        if (ent.Comp.Spent)
            args.PushMarkup(Loc.GetString("cult-soulstone-spent"));
    }

    private void OnStoneTerminating(Entity<CultSoulstoneComponent> ent, ref EntityTerminatingEvent args)
    {
        if (StoneShade(ent) is { } shade)
        {
            _container.Remove(shade, Slot(ent), force: true);
            QueueDel(shade);
        }
    }

    /// <summary>attack(): захват души трупа.</summary>
    private void OnStoneInteract(Entity<CultSoulstoneComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;
        var user = args.User;

        if (HasComp<CultShadeComponent>(target))
        {
            args.Handled = true;
            CaptureShade(ent, target, user);
            return;
        }

        if (!HasComp<HumanoidProfileComponent>(target))
            return;
        args.Handled = true;

        if (!RoleCheck(ent, user))
        {
            Wracked(user);
            return;
        }
        if (ent.Comp.Spent)
        {
            _cult.Message(user, Loc.GetString("cult-soulstone-no-power", ("stone", ent)));
            return;
        }
        if (target == user)
            return;
        if (_cult.IsCultist(target) && _cult.IsCultist(user))
        {
            _cult.Message(user, Loc.GetString("cult-soulstone-brethren"));
            return;
        }
        if (ent.Comp.Purified && _cult.IsCultist(user))
        {
            HotPotato(ent, user);
            return;
        }

        CaptureSoul(ent, target, user);
    }

    private void HotPotato(EntityUid stone, EntityUid user)
    {
        _cult.Message(user, Loc.GetString("cult-soulstone-holy-burn", ("stone", stone)));
        _cult.Damage(user, "Heat", 10);
        _hands.TryDrop(user, stone, checkActionBlocker: false);
    }

    /// <summary>capture_soul().</summary>
    public bool CaptureSoul(EntityUid stoneUid, EntityUid victim, EntityUid user, bool forced = false)
    {
        if (!TryComp<CultSoulstoneComponent>(stoneUid, out var stone) || StoneShade(stoneUid) != null)
            return false;
        var ent = (stoneUid, stone);

        if (!forced)
        {
            if (_mind.TryGetMind(victim, out var victimMind, out _) && _rule.IsSacrificeTarget(victimMind))
            {
                _cult.Message(user, Loc.GetString("cult-soulstone-sacrifice-target"));
                return false;
            }
            if (!_mobState.IsDead(victim))
            {
                _cult.Message(user, Loc.GetString("cult-soulstone-capture-failed"));
                _cult.Message(user, Loc.GetString("cult-soulstone-kill-first"));
                return false;
            }
        }

        var shade = Spawn("MobCultShade", Transform(stoneUid).Coordinates);
        _cult.SetName(shade, Loc.GetString("cult-shade-name", ("name", Name(victim))));
        _container.Insert(shade, Slot(stoneUid), force: true);
        if (TryComp<CultShadeComponent>(shade, out var shadeComp))
            shadeComp.Master = user;

        if (_mind.TryGetMind(victim, out var mindId, out var mind))
        {
            _mind.TransferTo(mindId, shade, mind: mind);
            if (_cult.IsCultist(user))
            {
                _cult.AddCultist(shade, silent: true);
                _cult.Message(shade, Loc.GetString("cult-soulstone-captured-cult"));
            }
            else
            {
                _cult.Message(shade, Loc.GetString("cult-soulstone-captured-master", ("master", user)));
            }
            if (!forced)
                _cult.Message(user, Loc.GetString("cult-soulstone-capture-success", ("target", victim), ("stone", stoneUid)));
        }
        else
        {
            _cult.Message(user, Loc.GetString("cult-soulstone-capture-failed"));
            _cult.Message(user, Loc.GetString("cult-soulstone-fled"));
            _cult.MakeGhostRole(shade, Name(shade));
            if (_cult.IsCultist(user))
                _cult.AddCultist(shade, silent: true);
        }

        UpdateStone(ent);
        if (!forced)
            _structures.DustBody(victim);
        return true;
    }

    private void CaptureShade(Entity<CultSoulstoneComponent> ent, EntityUid shade, EntityUid user)
    {
        if (!RoleCheck(ent, user))
        {
            Wracked(user);
            return;
        }
        if (StoneShade(ent) != null)
        {
            _cult.Message(user, Loc.GetString("cult-soulstone-capture-failed"));
            _cult.Message(user, Loc.GetString("cult-soulstone-full", ("stone", ent)));
            return;
        }
        if (TryComp<CultShadeComponent>(shade, out var captured))
            captured.ReleaseTime = null;
        _container.Insert(shade, Slot(ent), force: true);
        _cult.Message(shade, Loc.GetString("cult-soulstone-shade-captured", ("stone", ent)));
        if (user != shade)
            _cult.Message(user, Loc.GetString("cult-soulstone-shade-success", ("target", shade), ("stone", ent)));
        UpdateStone(ent);
    }

    /// <summary>attack_self(): выпустить тень.</summary>
    private void OnStoneUse(Entity<CultSoulstoneComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var user = args.User;
        if (!RoleCheck(ent, user))
        {
            Wracked(user);
            return;
        }
        if (ent.Comp.Purified && _cult.IsCultist(user))
        {
            HotPotato(ent, user);
            return;
        }
        ReleaseShades(ent, user);
    }

    public void ReleaseShades(EntityUid stoneUid, EntityUid user, bool silent = false)
    {
        if (!TryComp<CultSoulstoneComponent>(stoneUid, out var stone) || StoneShade(stoneUid) is not { } shade)
            return;
        _container.Remove(shade, Slot(stoneUid), force: true);
        _transform.SetCoordinates(shade, Transform(user).Coordinates);
        if (TryComp<CultShadeComponent>(shade, out var shadeComp))
            shadeComp.ReleaseTime = _timing.CurTime;
        _transform.AttachToGridOrMap(shade);
        if (!silent)
        {
            _cult.Message(shade, _cult.IsCultist(user)
                ? Loc.GetString("cult-soulstone-released-cult")
                : Loc.GetString("cult-soulstone-released-master", ("master", user)));
        }
        UpdateStone((stoneUid, stone));
    }

    /// <summary>soulstone/corrupt(): очищенный камень становится камнем культа.</summary>
    public bool CorruptStone(Entity<CultSoulstoneComponent> ent)
    {
        if (!ent.Comp.Purified)
            return false;
        ent.Comp.Purified = false;
        if (StoneShade(ent) is { } shade && !_cult.IsCultist(shade))
            _cult.AddCultist(shade, silent: true);
        UpdateStone(ent);
        return true;
    }

    #endregion

    #region Оболочка

    private void OnShellExamined(Entity<CultConstructShellComponent> ent, ref ExaminedEvent args)
    {
        if (_cult.IsCultist(args.Examiner) || HasComp<Content.Shared.Ghost.GhostComponent>(args.Examiner))
            args.PushMarkup(Loc.GetString("cult-shell-examine"));
    }

    private void OnShellInteractUsing(Entity<CultConstructShellComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<CultSoulstoneComponent>(args.Used, out var stone))
            return;
        args.Handled = true;
        var user = args.User;

        if (!_cult.IsCultist(user) && !stone.Purified)
        {
            _cult.Message(user, Loc.GetString("cult-shell-dread", ("stone", args.Used)));
            return;
        }
        if (stone.Purified && _cult.IsCultist(user))
        {
            HotPotato(args.Used, user);
            return;
        }

        var stoneUid = args.Used;
        if (StoneShade(stoneUid) is not { } shade)
        {
            _cult.Message(user, Loc.GetString("cult-shell-empty", ("stone", stoneUid)));
            return;
        }

        var shell = ent.Owner;
        OpenConstructChoice(user, type =>
        {
            if (TerminatingOrDeleted(shell) || TerminatingOrDeleted(stoneUid) || !_hands.IsHolding(user, stoneUid))
                return;
            var coords = Transform(shell).Coordinates;
            _container.Remove(shade, Slot(stoneUid), force: true);
            _cult.RemoveCultist(shade);
            MakeConstruct(type, shade, user, coords, fromMind: shade);
            QueueDel(shade);
            QueueDel(shell);
            QueueDel(stoneUid);
        });
    }

    #endregion

    #region Снаряды

    /// <summary>Кровавый залп: культистов лечит и проходит насквозь.</summary>
    private void OnBoltHit(Entity<CultBloodBoltComponent> ent, ref ProjectileHitEvent args)
    {
        var target = args.Target;
        _audio.PlayPvs(CultSounds.Splat, target);
        _cult.Effect("CultEffectSparks", Transform(target).Coordinates, randomDir: true);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateMissiles();
        var now = _timing.CurTime;
        var ended = new List<(EntityUid, CultPhasedComponent)>();
        var query = EntityQueryEnumerator<CultPhasedComponent>();
        while (query.MoveNext(out var uid, out var phased))
        {
            // phased_check(): святая земля не пускает.
            var here = Transform(uid).Coordinates;
            if (_cult.IsBlessed(here))
            {
                if (phased.LastSafe is { } safe)
                    _transform.SetCoordinates(uid, safe);
                _cult.Message(uid, Loc.GetString("cult-blessed-block"));
            }
            else
            {
                phased.LastSafe = here;
            }

            if (now < phased.End)
                continue;
            // Нельзя выйти внутри стены: ждём свободного тайла.
            if (_structures.IsDense(Transform(uid).Coordinates))
            {
                phased.End = now + TimeSpan.FromSeconds(0.5);
                continue;
            }
            ended.Add((uid, phased));
        }

        foreach (var (uid, phased) in ended)
        {
            SetPhased(uid, phased, false);
            RemComp<CultPhasedComponent>(uid);
            _cult.Effect("CultEffectWraithIn", Transform(uid).Coordinates, Transform(uid).LocalRotation);
            _audio.PlayPvs(CultSounds.EtherealExit, uid, AudioParams.Default.WithVolume(CultSystem.Db(50)));
        }
    }
}

internal static class CultObjectExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
