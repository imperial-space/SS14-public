using System.Numerics;
using Content.Shared.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Gravity;
using Content.Shared.Imperial.Uplink.AssaultPod;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Uplink.AssaultPod;

/// <summary>Консоль большого штурмового пода «Steel Rain» (computer/shuttle/syndicate/drop_pod).</summary>
[RegisterComponent]
public sealed partial class AssaultPodConsoleComponent : Component
{
    [DataField]
    public TimeSpan Countdown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan FlightTime = TimeSpan.FromSeconds(20);

    /// <summary>false — экипаж выбирает только зону, а старт даёт сценарий (протокол «Эпсилон»).</summary>
    [DataField]
    public bool ManualLaunch = true;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public string? TargetName;

    [ViewVariables]
    public AssaultPodState State = AssaultPodState.Ready;

    [ViewVariables]
    public TimeSpan NextStage;
}

/// <summary>Стиль большого пода: ЯО, ОБР или эскадрон смерти.</summary>
public enum AssaultPodShipStyle : byte
{
    Syndicate,
    Ert,
    DeathSquad,
    Epsilon,
}

/// <summary>
/// Большие штурмовые поды Paradise (assault_pod.dm, «Steel Rain» на centcomm.dmm): отдельный грид 7×7.
/// Консоль выбирает маяк станции; под летит через гиперпространство и врезается в станцию:
/// на месте посадки всё сносится, а клетки и содержимое пода становятся частью станции.
/// </summary>
public sealed class AssaultPodShipSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly Robust.Shared.Random.IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;
    [Dependency] private readonly AssaultPodSystem _pods = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly AssaultPodEffectsSystem _effects = default!;

    private static readonly SoundSpecifier AlarmSound = new SoundPathSpecifier("/Audio/Machines/Nuke/general_beep.ogg");
    private static readonly SoundSpecifier LaunchSound = new SoundPathSpecifier("/Audio/Imperial/Uplink/podwoosh.ogg");
    private static readonly SoundSpecifier LandSound = new SoundPathSpecifier("/Audio/Effects/explosion1.ogg");

    private const int Size = 7;
    private static readonly Vector2i Center = new(3, 3);
    private const float CrushDamage = 200f;
    private const float KnockdownRange = 8f;

    private VerbCategory? _landingCategory;
    private readonly HashSet<EntityUid> _pendingAtmos = new();

    /// <summary>
    /// Раскладка Steel Rain сверху вниз (y = 6..0): W — стена, F — пол, A — шлюз, T — угловая турель на своей клетке (только у Синдиката, у остальных пусто),
    /// K — консоль, E/W/N/S после «C» — кресло, смотрящее в эту сторону, «.» — пусто.
    /// </summary>
    private static readonly string[][] Layout =
    {
        new[] { "T", "W", "W", "A", "W", "W", "T" },
        new[] { "W", "W", "CE", "F", "CW", "W", "W" },
        new[] { "W", "CE", "F", "CN", "F", "CW", "W" },
        new[] { "A", "F", "CW", "K", "CE", "F", "A" },
        new[] { "W", "CE", "F", "CS", "F", "CW", "W" },
        new[] { "W", "W", "CE", "F", "CW", "W", "W" },
        new[] { "T", "W", "W", "A", "W", "W", "T" },
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AssaultPodConsoleComponent, GetVerbsEvent<Verb>>(OnConsoleVerbs);
        SubscribeLocalEvent<AssaultPodConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnConsoleAltVerbs);
        SubscribeLocalEvent<AssaultPodConsoleComponent, ExaminedEvent>(OnConsoleExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingAtmos.Count > 0)
        {
            foreach (var grid in new List<EntityUid>(_pendingAtmos))
            {
                if (TerminatingOrDeleted(grid)
                    || !TryComp<GridAtmosphereComponent>(grid, out var gridAtmos)
                    || !TryComp<MapGridComponent>(grid, out var mapGrid))
                {
                    _pendingAtmos.Remove(grid);
                    continue;
                }

                if (gridAtmos.Tiles.Count == 0)
                    continue;

                _atmos.RebuildGridAtmosphere((grid, gridAtmos, mapGrid));
                _pendingAtmos.Remove(grid);
            }
        }

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AssaultPodConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.State == AssaultPodState.Launching)
                _effects.UpdateCountdown(uid, console.NextStage, grid: true);

            if (console.State == AssaultPodState.Flying && _effects.ShouldBeginArrival(uid, console.NextStage))
                BeginArrival((uid, console));

            if (now < console.NextStage)
                continue;

            switch (console.State)
            {
                case AssaultPodState.Launching:
                    TakeOff((uid, console), now);
                    break;
                case AssaultPodState.Flying:
                    Land((uid, console));
                    break;
            }
        }
    }

    #region Постройка

    /// <summary>Админ-спавн пода у точки; если место занято гридом, сдвигаем на восток.</summary>
    public EntityUid SpawnShip(AssaultPodShipStyle style, MapCoordinates near)
    {
        var origin = near.Position - new Vector2(Size / 2f, Size / 2f);
        for (var i = 0; i < 20; i++)
        {
            var box = new Box2(origin - new Vector2(1, 1), origin + new Vector2(Size + 1, Size + 1));
            var blocked = false;
            foreach (var _ in _mapManager.FindGridsIntersecting(near.MapId, box))
            {
                blocked = true;
                break;
            }

            if (!blocked)
                break;

            origin += new Vector2(Size + 3, 0);
        }

        var mapUid = _map.GetMap(near.MapId);
        var gridEnt = _mapManager.CreateGridEntity(near.MapId);
        var grid = gridEnt.Owner;
        _transform.SetCoordinates(grid, new EntityCoordinates(mapUid, origin));
        _meta.SetEntityName(grid, Loc.GetString(style switch
        {
            AssaultPodShipStyle.Ert => "assault-pod-ship-name-ert",
            AssaultPodShipStyle.DeathSquad => "assault-pod-ship-name-deathsquad",
            AssaultPodShipStyle.Epsilon => "assault-pod-ship-name-epsilon",
            _ => "assault-pod-ship-name-syndicate",
        }));

        var (wall, floor, airlock, console) = style switch
        {
            AssaultPodShipStyle.Ert => ("WallShuttle", "FloorShuttleBlue", "AirlockCentralCommandLocked", "ImperialAssaultPodConsoleErt"),
            AssaultPodShipStyle.DeathSquad => ("WallPlastitanium", "FloorShuttleBlack", "AirlockCentralCommandLocked", "ImperialAssaultPodConsoleDeathSquad"),
            AssaultPodShipStyle.Epsilon => ("WallPlastitanium", "FloorShuttleBlack", "AirlockCentralCommandLocked", "ImperialAssaultPodConsoleEpsilon"),
            _ => ("WallPlastitanium", "FloorShuttleRed", "AirlockShuttleSyndicate", "ImperialAssaultPodConsoleSyndicate"),
        };

        var floorTile = new Tile(_tiles[floor].TileId);
        var plating = new Tile(_tiles["Plating"].TileId);
        var tiles = new List<(Vector2i, Tile)>();
        for (var row = 0; row < Size; row++)
        {
            for (var x = 0; x < Size; x++)
            {
                var cell = Layout[row][x];
                if (cell == "." || cell == "T" && style != AssaultPodShipStyle.Syndicate)
                    continue;

                var y = Size - 1 - row;
                var useFloor = cell != "W" && cell != "T" && cell != "A";
                tiles.Add((new Vector2i(x, y), useFloor ? floorTile : plating));
            }
        }

        _map.SetTiles(gridEnt, tiles);

        for (var row = 0; row < Size; row++)
        {
            for (var x = 0; x < Size; x++)
            {
                var cell = Layout[row][x];
                var y = Size - 1 - row;
                switch (cell)
                {
                    case "W":
                        SpawnAt(grid, wall, x, y);
                        break;
                    case "T":
                        // porta_turret/syndicate/pod: по турели в каждом внешнем углу — только у Синдиката.
                        if (style == AssaultPodShipStyle.Syndicate)
                            SpawnAt(grid, "ImperialAssaultPodTurret", x, y);
                        break;
                    case "A":
                        SpawnAt(grid, airlock, x, y, x == 0 || x == Size - 1 ? Angle.FromDegrees(90) : null);
                        break;
                    case "K":
                        SpawnAt(grid, console, x, y);
                        break;
                    case "CE":
                        SpawnAt(grid, "ChairPilotSeat", x, y, Direction.East.ToAngle());
                        break;
                    case "CW":
                        SpawnAt(grid, "ChairPilotSeat", x, y, Direction.West.ToAngle());
                        break;
                    case "CN":
                        SpawnAt(grid, "ChairPilotSeat", x, y, Direction.North.ToAngle());
                        break;
                    case "CS":
                        SpawnAt(grid, "ChairPilotSeat", x, y, Direction.South.ToAngle());
                        break;
                }
            }
        }

        SpawnAt(grid, "AlwaysPoweredWallLight", 3, 5);
        SpawnAt(grid, "AlwaysPoweredWallLight", 3, 1, Angle.FromDegrees(180));

        // Своя сеть, как у шаттлов: вспомогательный генератор LV в западной стене и кабель на каждой клетке.
        SpawnAt(grid, "GeneratorWallmountAPULV", 0, 4, Direction.East.ToAngle());
        foreach (var (indices, _) in tiles)
        {
            SpawnAt(grid, "CableApcExtension", indices.X, indices.Y);
        }

        var gravity = EnsureComp<GravityComponent>(grid);
        gravity.Enabled = true;
        Dirty(grid, gravity);

        if (TryComp<PhysicsComponent>(grid, out var body))
            _physics.SetBodyType(grid, BodyType.Static, body: body);

        _pendingAtmos.Add(grid);
        return grid;
    }

    private EntityUid SpawnAt(EntityUid grid, string proto, int x, int y, Angle? rotation = null)
    {
        var ent = Spawn(proto, new EntityCoordinates(grid, x + 0.5f, y + 0.5f));
        if (rotation is { } rot)
            _transform.SetLocalRotation(ent, rot);

        return ent;
    }

    #endregion

    #region Консоль

    private void OnConsoleExamined(Entity<AssaultPodConsoleComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.ManualLaunch && ent.Comp.State == AssaultPodState.Ready)
            args.PushMarkup(Loc.GetString("assault-pod-examine-scripted"));

        args.PushMarkup(ent.Comp.State == AssaultPodState.Landed
            ? Loc.GetString("assault-pod-examine-landed")
            : ent.Comp.TargetName == null
                ? Loc.GetString("assault-pod-ship-no-zone")
                : Loc.GetString("assault-pod-examine-zone", ("zone", ent.Comp.TargetName)));
    }

    /// <summary>possibleDestinations2: зоны высадки — маяки станции.</summary>
    private void OnConsoleVerbs(Entity<AssaultPodConsoleComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.State != AssaultPodState.Ready)
            return;

        _landingCategory ??= new VerbCategory("assault-pod-verb-category", null);
        var user = args.User;
        foreach (var (beacon, name) in _pods.GetLandingZones())
        {
            args.Verbs.Add(new Verb
            {
                Text = name,
                Category = _landingCategory,
                Act = () =>
                {
                    ent.Comp.Target = beacon;
                    ent.Comp.TargetName = name;
                    _popup.PopupEntity(Loc.GetString("assault-pod-zone-set", ("zone", name)), user, user);
                },
            });
        }
    }

    private void OnConsoleAltVerbs(Entity<AssaultPodConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.State != AssaultPodState.Ready || !ent.Comp.ManualLaunch)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("assault-pod-ship-launch-verb", ("zone", ent.Comp.TargetName ?? "?")),
            Priority = 2,
            Act = () => StartLaunch(ent, user),
        });
    }

    /// <summary>Админ-запуск: консоль на гриде пода, маяк по части названия (или уже выбранный, или случайный).</summary>
    public string LaunchFromCommand(EntityUid? grid, string? zone)
    {
        var launched = 0;
        var query = EntityQueryEnumerator<AssaultPodConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var console, out var xform))
        {
            if (grid != null && xform.GridUid != grid)
                continue;

            if (console.State != AssaultPodState.Ready)
            {
                if (grid == null)
                    continue;

                return Loc.GetString("assault-pod-command-not-ready");
            }

            var zones = _pods.GetLandingZones();
            if (zones.Count == 0)
                return Loc.GetString("assault-pod-no-zones");

            if (zone != null)
            {
                var found = zones.FindIndex(z => z.Name.Contains(zone, StringComparison.OrdinalIgnoreCase));
                if (found < 0)
                    return Loc.GetString("assault-pod-command-no-zone", ("zone", zone));

                (console.Target, console.TargetName) = zones[found];
            }
            else if (console.Target == null)
            {
                (console.Target, console.TargetName) = _random.Pick(zones);
            }

            console.State = AssaultPodState.Launching;
            console.NextStage = _timing.CurTime + console.Countdown;
            _audio.PlayPvs(AlarmSound, uid);
            _popup.PopupEntity(Loc.GetString("assault-pod-ship-countdown", ("seconds", (int) console.Countdown.TotalSeconds)), uid, PopupType.LargeCaution);
            launched++;
            if (grid != null)
                return Loc.GetString("assault-pod-command-dropped", ("zone", console.TargetName ?? "?"));
        }

        return launched > 0
            ? Loc.GetString("assault-pod-command-launched-all", ("count", launched))
            : Loc.GetString("assault-pod-command-no-console");
    }

    /// <summary>Старт по сценарию: зона — выбранная экипажем, иначе случайный маяк станции.</summary>
    public bool ForceLaunch(Entity<AssaultPodConsoleComponent> ent)
    {
        if (ent.Comp.State != AssaultPodState.Ready)
            return false;

        if (ent.Comp.Target == null || TerminatingOrDeleted(ent.Comp.Target.Value))
        {
            var zones = _pods.GetLandingZones();
            if (zones.Count == 0)
                return false;

            (ent.Comp.Target, ent.Comp.TargetName) = _random.Pick(zones);
        }

        ent.Comp.State = AssaultPodState.Launching;
        ent.Comp.NextStage = _timing.CurTime + ent.Comp.Countdown;
        _audio.PlayPvs(AlarmSound, ent);
        _popup.PopupEntity(Loc.GetString("assault-pod-ship-countdown", ("seconds", (int) ent.Comp.Countdown.TotalSeconds)), ent, PopupType.LargeCaution);
        return true;
    }

    private void StartLaunch(Entity<AssaultPodConsoleComponent> ent, EntityUid user)
    {
        if (ent.Comp.Target == null || TerminatingOrDeleted(ent.Comp.Target.Value))
        {
            _popup.PopupEntity(Loc.GetString("assault-pod-ship-no-zone"), user, user);
            return;
        }

        if (Transform(ent).GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out _))
            return;

        ent.Comp.State = AssaultPodState.Launching;
        ent.Comp.NextStage = _timing.CurTime + ent.Comp.Countdown;
        _audio.PlayPvs(AlarmSound, ent);
        _popup.PopupEntity(Loc.GetString("assault-pod-ship-countdown", ("seconds", (int) ent.Comp.Countdown.TotalSeconds)), ent, PopupType.LargeCaution);
    }

    #endregion

    #region Полёт и посадка

    private void TakeOff(Entity<AssaultPodConsoleComponent> ent, TimeSpan now)
    {
        if (Transform(ent).GridUid is not { } grid)
            return;

        _audio.PlayPvs(LaunchSound, ent);
        ent.Comp.State = AssaultPodState.Flying;
        ent.Comp.NextStage = now + ent.Comp.FlightTime;
        _effects.StartTravel(ent, grid: true);

        // Через гиперпространство, как капсулы и шаттлы в FTL.
        var transit = _pods.GetTransitSpot();
        _transform.SetCoordinates(grid, new EntityCoordinates(_map.GetMap(transit.MapId), transit.Position));
    }

    /// <summary>За 5 с до посадки: метки в центре и по углам будущего пятна посадки.</summary>
    private void BeginArrival(Entity<AssaultPodConsoleComponent> ent)
    {
        if (ent.Comp.Target == null || TerminatingOrDeleted(ent.Comp.Target.Value))
        {
            var zones = _pods.GetLandingZones();
            ent.Comp.Target = zones.Count > 0 ? _random.Pick(zones).Beacon : null;
        }

        var spots = new List<EntityCoordinates>();
        if (ent.Comp.Target is { } target && Transform(target).GridUid is { } station && TryComp<MapGridComponent>(station, out var stationGrid))
        {
            var landing = _map.TileIndicesFor(station, stationGrid, Transform(target).Coordinates);
            var half = Size / 2;
            foreach (var offset in new[] { Vector2i.Zero, new Vector2i(-half, -half), new Vector2i(half, -half), new Vector2i(-half, half), new Vector2i(half, half) })
                spots.Add(_map.GridTileToLocal(station, stationGrid, landing + offset));
        }

        _effects.BeginArrival(ent, true, spots, ent.Comp.NextStage);
    }

    /// <summary>
    /// dock() на маяк: стены, машины и всё прочее на месте посадки сносится, стоящие там — раздавлены,
    /// клетки пода вписываются в грид станции, а всё его содержимое переносится на станцию.
    /// </summary>
    private void Land(Entity<AssaultPodConsoleComponent> ent)
    {
        if (Transform(ent).GridUid is not { } pod || !TryComp<MapGridComponent>(pod, out var podGrid))
            return;

        var target = ent.Comp.Target;
        if (target == null || TerminatingOrDeleted(target.Value))
        {
            var zones = _pods.GetLandingZones();
            target = zones.Count > 0 ? _random.Pick(zones).Beacon : null;
        }

        if (target == null
            || Transform(target.Value).GridUid is not { } station
            || !TryComp<MapGridComponent>(station, out var stationGrid))
        {
            _effects.ResetArrival(ent);
            ent.Comp.NextStage = _timing.CurTime + TimeSpan.FromSeconds(5);
            return;
        }

        var landing = _map.TileIndicesFor(station, stationGrid, Transform(target.Value).Coordinates);

        // Снос всего, что стоит на месте посадки.
        var podTiles = new List<(Vector2i Local, Tile Tile)>();
        var enumerator = _map.GetAllTilesEnumerator(pod, podGrid);
        while (enumerator.MoveNext(out var tileRef))
        {
            podTiles.Add((tileRef.Value.GridIndices, tileRef.Value.Tile));
        }

        var crushed = new HashSet<EntityUid>();
        var stationTiles = new List<(Vector2i, Tile)>();
        foreach (var (local, tile) in podTiles)
        {
            var indices = landing + (local - Center);
            stationTiles.Add((indices, tile));

            foreach (var anchored in _map.GetAnchoredEntities((station, stationGrid), indices))
            {
                if (anchored != target.Value)
                    QueueDel(anchored);
            }

            foreach (var other in _lookup.GetLocalEntitiesIntersecting(station, indices, 0.1f, LookupFlags.Dynamic | LookupFlags.Sundries))
            {
                if (!HasComp<MobStateComponent>(other) || !crushed.Add(other))
                    continue;

                var crush = new DamageSpecifier();
                crush.DamageDict["Blunt"] = CrushDamage;
                _damageable.TryChangeDamage(other, crush, origin: ent);
            }
        }

        _map.SetTiles(station, stationGrid, stationTiles);

        // Всё, что было на гриде пода (десант, кресла, турели, консоль), переезжает на станцию.
        var children = new List<EntityUid>();
        var childEnum = Transform(pod).ChildEnumerator;
        while (childEnum.MoveNext(out var child))
        {
            children.Add(child);
        }

        var moved = new HashSet<EntityUid>();
        var podCenter = (Vector2) Center + new Vector2(0.5f, 0.5f);
        var landingCenter = (Vector2) landing + new Vector2(0.5f, 0.5f);
        foreach (var child in children)
        {
            if (TerminatingOrDeleted(child) || HasComp<MapGridComponent>(child))
                continue;

            var xform = Transform(child);
            var wasAnchored = xform.Anchored;
            var localPos = xform.LocalPosition;
            var localRot = xform.LocalRotation;

            if (wasAnchored)
                _transform.Unanchor(child, xform, setPhysics: false);

            _transform.SetCoordinates(child, new EntityCoordinates(station, landingCenter + (localPos - podCenter)));
            _transform.SetLocalRotation(child, localRot);

            if (wasAnchored)
                _transform.AnchorEntity(child);

            moved.Add(child);
        }

        QueueDel(pod);

        ent.Comp.State = AssaultPodState.Landed;
        Log.Info($"Большой штурмовой под приземлился у маяка {ent.Comp.TargetName} на {ToPrettyString(station)} в {landing}");

        var coords = new EntityCoordinates(station, landingCenter);
        _audio.PlayPvs(LandSound, coords, AudioParams.Default.WithMaxDistance(30f));
        _effects.Landed(ent, coords);
        foreach (var other in _lookup.GetEntitiesInRange(coords, KnockdownRange))
        {
            if (moved.Contains(other) || !HasComp<MobStateComponent>(other))
                continue;

            _stun.TryKnockdown(other, TimeSpan.FromSeconds(3));
        }
    }

    #endregion
}
