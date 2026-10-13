using System.Linq;
using System.Numerics;
using Content.Server.Imperial.Xenobiology.Components;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Physics;
using Content.Shared.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Xenobiology;

/// <summary>
/// Прочие предметы ксенобиологии: граната-кластер слаймов, блюспейс кристалл, остановка времени,
/// блюспейс и сепия плитка, церулеанские чертежи.
/// </summary>
public sealed class SlimeItemsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SlimeExtractSystem _extract = default!;
    [Dependency] private readonly SlimeSpeedModifierSystem _floorSpeed = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly SoundSpecifier Bubbles = new SoundPathSpecifier("/Audio/Imperial/Xenobiology/bubbles.ogg");
    private static readonly SoundSpecifier Blink = new SoundCollectionSpecifier("sparks");
    private static readonly SoundSpecifier TimestopSound = new SoundPathSpecifier("/Audio/Imperial/Xenobiology/timestop.ogg");
    private static readonly EntProtoId SegmentProto = "SlimeClusterGrenadeSegment";
    private const string FloorKey = "floor";

    /// <summary>turf/open/floor/bluespace (slowdown -1) и turf/open/floor/sepia (slowdown 2).</summary>
    private static readonly Dictionary<string, float> FloorSpeed = new()
    {
        { "FloorSlimeBluespace", 2f },
        { "FloorSlimeSepia", 0.45f },
    };

    private readonly List<(TimeSpan At, Action Act)> _scheduled = new();
    private List<EntProtoId>? _extracts;
    private TimeSpan _nextFloorCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeGrenadeComponent, UseInHandEvent>(OnGrenadeUse);
        SubscribeLocalEvent<SlimeGrenadeComponent, SlimeGrenadeDetonateEvent>(OnGrenadeDetonate);
        SubscribeLocalEvent<SlimeBluespaceCrystalComponent, UseInHandEvent>(OnCrystalUse);
        SubscribeLocalEvent<SlimeBluespaceCrystalComponent, ThrowDoHitEvent>(OnCrystalHit);
        SubscribeLocalEvent<SlimeTimestopComponent, MapInitEvent>(OnTimestopInit);
        SubscribeLocalEvent<SlimeBlueprintComponent, UseInHandEvent>(OnBlueprintUse);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _extracts = null);
    }

    private void Schedule(TimeSpan delay, Action act)
    {
        _scheduled.Add((_timing.CurTime + delay, act));
    }

    #region Граната

    private void OnGrenadeUse(Entity<SlimeGrenadeComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Armed)
            return;
        args.Handled = true;
        Arm(ent, TimeSpan.FromSeconds(ent.Comp.Delay));
        _popup.PopupEntity(Loc.GetString("slime-grenade-armed", ("grenade", ent.Owner)), args.User, args.User);
    }

    public void Arm(Entity<SlimeGrenadeComponent> ent, TimeSpan delay)
    {
        ent.Comp.Armed = true;
        _appearance.SetData(ent, SlimeGrenadeVisuals.Active, true);
        Schedule(delay, () =>
        {
            if (!TerminatingOrDeleted(ent))
                RaiseLocalEvent(ent, new SlimeGrenadeDetonateEvent());
        });
    }

    /// <summary>clusterbuster/detonate(): сегменты и случайные экстракты.</summary>
    private void OnGrenadeDetonate(Entity<SlimeGrenadeComponent> ent, ref SlimeGrenadeDetonateEvent args)
    {
        var coords = _transform.GetMoverCoordinates(ent);
        var count = _random.Next(ent.Comp.MinSpawned, ent.Comp.MaxSpawned + 1);

        if (!ent.Comp.Segment)
        {
            var again = 0;
            for (var i = 0; i < count; i++)
            {
                if (!_random.Prob(ent.Comp.SegmentChance))
                    continue;
                again++;
            }

            count -= again;
            for (var i = 0; i < again; i++)
            {
                var segment = Spawn(SegmentProto, Scatter(coords));
                if (!TryComp<SlimeGrenadeComponent>(segment, out var segmentComp))
                    continue;
                segmentComp.Volatile = ent.Comp.Volatile;
                segmentComp.MinSpawned = ent.Comp.MinSpawned;
                segmentComp.MaxSpawned = ent.Comp.MaxSpawned;
                Arm((segment, segmentComp), RandomDetonate());
            }
        }

        SpawnPayload(coords, count, ent.Comp.Volatile);
        _audio.PlayPvs(Bubbles, coords);
        QueueDel(ent);
    }

    /// <summary>payload_spawner/random_slime.</summary>
    private void SpawnPayload(EntityCoordinates coords, int count, bool isVolatile)
    {
        var extracts = Extracts();
        if (extracts.Count == 0)
            return;

        for (var i = 0; i < count; i++)
        {
            var extract = Spawn(_random.Pick(extracts), Scatter(coords));
            if (!isVolatile || !TryComp<SlimeExtractComponent>(extract, out var extractComp))
                continue;
            Schedule(RandomDetonate(), () => _extract.AutoActivate((extract, extractComp)));
        }
    }

    private TimeSpan RandomDetonate()
    {
        return TimeSpan.FromSeconds(_random.NextFloat(1.5f, 6f));
    }

    private EntityCoordinates Scatter(EntityCoordinates coords)
    {
        var steps = _random.Next(1, 5);
        return coords.Offset(_random.NextAngle().ToVec() * steps * 0.5f);
    }

    private List<EntProtoId> Extracts()
    {
        return _extracts ??= _proto.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && p.ID.StartsWith("SlimeExtract") && p.HasComponent<SlimeExtractComponent>(EntityManager.ComponentFactory)
                        && !p.HasComponent<SlimeCrossbreedComponent>(EntityManager.ComponentFactory))
            .Select(p => new EntProtoId(p.ID))
            .ToList();
    }

    #endregion

    #region Блюспейс кристалл

    private void OnCrystalUse(Entity<SlimeBluespaceCrystalComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("slime-bluespace-crystal-crush", ("crystal", ent.Owner)), args.User, args.User);
        BlinkMob(args.User, ent.Comp.BlinkRange);
        UseOne(ent);
    }

    private void OnCrystalHit(Entity<SlimeBluespaceCrystalComponent> ent, ref ThrowDoHitEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Target))
            return;
        BlinkMob(args.Target, ent.Comp.BlinkRange);
        UseOne(ent);
    }

    private void UseOne(EntityUid uid)
    {
        if (TryComp<StackComponent>(uid, out var stack))
            _stack.TryUse((uid, stack), 1);
        else
            QueueDel(uid);
    }

    /// <summary>do_teleport(): случайная свободная клетка в радиусе.</summary>
    public void BlinkMob(EntityUid mob, int range)
    {
        var xform = Transform(mob);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var origin = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var tile = origin + new Vector2i(_random.Next(-range, range + 1), _random.Next(-range, range + 1));
            if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || _turf.IsSpace(tileRef)
                || _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable))
                continue;

            _audio.PlayPvs(Blink, xform.Coordinates);
            _transform.SetCoordinates(mob, _map.GridTileToLocal(gridUid, grid, tile));
            _transform.AttachToGridOrMap(mob);
            _audio.PlayPvs(Blink, mob);
            return;
        }
    }

    #endregion

    #region Остановка времени

    private void OnTimestopInit(Entity<SlimeTimestopComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.End = _timing.CurTime + ent.Comp.Duration;
        _audio.PlayPvs(TimestopSound, ent);
    }

    private void UpdateTimestop(TimeSpan now)
    {
        var query = EntityQueryEnumerator<SlimeTimestopComponent>();
        while (query.MoveNext(out var uid, out var stop))
        {
            if (now >= stop.End)
            {
                QueueDel(uid);
                continue;
            }

            if (now < stop.NextTick)
                continue;
            stop.NextTick = now + TimeSpan.FromSeconds(0.1);

            var remaining = stop.End - now;
            var coords = _transform.GetMapCoordinates(uid);
            foreach (var target in _lookup.GetEntitiesInRange(coords, stop.Range, LookupFlags.Dynamic | LookupFlags.Sundries))
            {
                if (stop.Immune.Contains(target))
                    continue;

                if (HasComp<MobStateComponent>(target))
                    _stun.TryUpdateParalyzeDuration(target, remaining);

                if (TryComp<PhysicsComponent>(target, out var physics) && physics.BodyType != BodyType.Static)
                {
                    _physics.SetLinearVelocity(target, Vector2.Zero, body: physics);
                    _physics.SetAngularVelocity(target, 0f, body: physics);
                }
            }
        }
    }

    #endregion

    #region Плитка

    /// <summary>Скорость существ на блюспейс и сепия плитке.</summary>
    private void UpdateFloors()
    {
        var query = EntityQueryEnumerator<InputMoverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            var modifier = 1f;
            if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid)
                && _map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tileRef)
                && FloorSpeed.TryGetValue(_turf.GetContentTileDefinition(tileRef).ID, out var speed))
            {
                modifier = speed;
            }

            if (!MathHelper.CloseTo(modifier, 1f) || _floorSpeed.HasModifier(uid, FloorKey))
                _floorSpeed.SetModifier(uid, FloorKey, modifier);
        }
    }

    #endregion

    #region Чертежи

    /// <summary>blueprints/slime/edit_area(): зона становится совместимой с консолью.</summary>
    private void OnBlueprintUse(Entity<SlimeBlueprintComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var coords = _transform.GetMoverCoordinates(args.User);
        var zone = Spawn(ent.Comp.Zone, coords);
        _transform.AnchorEntity(zone);
        _popup.PopupEntity(Loc.GetString("slime-blueprint-used"), args.User, args.User, PopupType.Medium);
        QueueDel(ent);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        for (var i = _scheduled.Count - 1; i >= 0; i--)
        {
            if (now < _scheduled[i].At)
                continue;
            var act = _scheduled[i].Act;
            _scheduled.RemoveAt(i);
            act();
        }

        UpdateTimestop(now);

        if (now >= _nextFloorCheck)
        {
            _nextFloorCheck = now + TimeSpan.FromSeconds(0.25);
            UpdateFloors();
        }
    }
}
