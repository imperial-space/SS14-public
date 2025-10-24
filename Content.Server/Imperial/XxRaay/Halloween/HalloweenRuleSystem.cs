using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Maps;
using Content.Server.Pinpointer;
using Content.Server.RoundEnd;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Pinpointer;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.XxRaay.Halloween;

public sealed class HalloweenRuleSystem : GameRuleSystem<HalloweenRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    private static readonly ISawmill Sawmill = Logger.GetSawmill("halloween_rule");

    private static readonly Regex ColorTagRegex =
        new(@"\[/?color(?:=[^\]]+)?\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private sealed class EventState
    {
        public EntityUid? Portal;
        public EntityUid? Queen;
        public int CurrentWave;
        public bool WaveEndScheduled;
        public bool WaveCompleted;
        public int MobsSpawned;
        public int TotalMobsToSpawn;
        public bool AllMobsSpawned;
    }

    // Per-rule runtime state
    private readonly Dictionary<EntityUid, EventState> _state = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HalloweenMobComponent, MobStateChangedEvent>(OnHalloweenMobStateChanged);
    }

    protected override void Started(EntityUid uid, HalloweenRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        Sawmill.Info("Halloween rule started.");
        component.Active = true;

        var st = new EventState { CurrentWave = 0, WaveEndScheduled = false, WaveCompleted = false, MobsSpawned = 0, TotalMobsToSpawn = 0, AllMobsSpawned = false };
        _state[uid] = st;

        if (TryGetRandomStation(out var chosenStation))
        {
            if (TryComp<StationDataComponent>(chosenStation, out var stationData))
            {
                var grid = _station.GetLargestGrid(stationData);
                if (grid != null)
                {
                    SpawnPortalOnRandomGridLocation(grid.Value, component.PortalPrototype);
                    // Find the spawned portal
                    var portalQuery = EntityQueryEnumerator<HalloweenPortalComponent>();
                    while (portalQuery.MoveNext(out var portalUid, out _))
                    {
                        st.Portal = portalUid;
                        break;
                    }
                    Sawmill.Info($"Halloween portal spawned on station grid {grid}.");

                    var portalLoc = GetLocationString(_transform.GetMapCoordinates(grid.Value));
                    var msgPortal = Loc.GetString("halloween-portal-spawn", ("loc", portalLoc));
                    _chatSystem.DispatchGlobalAnnouncement(msgPortal, "ЦентКом");
                }
            }
        }

        if (st.Portal == null)
        {
            Sawmill.Warning("Failed to find a valid position to spawn the Halloween portal.");
        }

        Timer.Spawn(component.TimeBetweenWaves, () => StartNextWave(uid, component, st));
    }

    protected override void Ended(EntityUid uid, HalloweenRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        Sawmill.Info("Halloween rule ended.");
        component.Active = false;

        if (_state.TryGetValue(uid, out var st))
        {
            if (st.Portal is { } portal && Exists(portal))
                QueueDel(portal);

            st.Portal = null;
            st.Queen = null;
        }

        CleanupHalloweenMobs();
        _state.Remove(uid);
    }

    private void StartNextWave(EntityUid ruleUid, HalloweenRuleComponent component, EventState st)
    {
        if (!component.Active)
            return;

        Sawmill.Debug("StartNextWave start");

        st.WaveEndScheduled = false;
        st.WaveCompleted = false;
        st.CurrentWave++;

        Sawmill.Debug($"Current wave: {st.CurrentWave}, Total waves: {component.Waves.Count}");

        if (component.Waves.Count == 0)
        {
            Sawmill.Warning("No waves configured in HalloweenRuleComponent!");
            return;
        }

        if (st.CurrentWave > component.Waves.Count)
        {
            if (component.QueenWave != null)
            {
                Sawmill.Debug("Timer before queen start");
                Timer.Spawn(component.TimeBeforeQueen, () => SpawnQueenSequence(ruleUid, component, st));
            }
            Sawmill.Debug("All waves completed");
            return;
        }

        var wave = component.Waves[st.CurrentWave - 1];
        var portalLocation = GetPortalLocationString(st);
        var announcement = GetWaveAnnouncement(st.CurrentWave, portalLocation, component);
        _chatSystem.DispatchGlobalAnnouncement(announcement, "ЦентКом");
        Sawmill.Debug("SpawnWaveRoutine");
        SpawnWaveRoutine(ruleUid, wave, component, st);
    }

    private void SpawnWaveRoutine(EntityUid ruleUid, HalloweenWave wave, HalloweenRuleComponent comp, EventState st)
    {
        Sawmill.Debug($"SpawnWaveRoutine called for wave {st.CurrentWave}");
        Sawmill.Debug($"Wave details - MobCount: {wave.MobCount}, WaveLength: {wave.WaveLength}, Prototypes: {string.Join(", ", wave.MobPrototypes)}");

        // Initialize wave state
        st.MobsSpawned = 0;
        st.TotalMobsToSpawn = wave.MobCount;
        st.AllMobsSpawned = false;

        ScheduleWaveEnd(ruleUid, comp, st, wave.WaveLength);

        if (wave.MobCount <= 0 || !wave.MobPrototypes.Any())
        {
            Sawmill.Warning($"Wave {st.CurrentWave} has no mobs to spawn (count: {wave.MobCount}, prototypes: {wave.MobPrototypes.Count})");
            st.AllMobsSpawned = true;
            return;
        }

        var interval = wave.WaveLength / wave.MobCount;
        var attemptsLeft = wave.MobCount;
        Sawmill.Debug("SpawnWaveRoutine");

        void SpawnNext()
        {
            if (!comp.Active || attemptsLeft <= 0)
            {
                Sawmill.Debug($"SpawnNext stopped - Active: {comp.Active}, attemptsLeft: {attemptsLeft}");
                st.AllMobsSpawned = true;
                return;
            }

            Sawmill.Debug($"SpawnNext - attemptsLeft: {attemptsLeft}, wave: {st.CurrentWave}");

            attemptsLeft--;
            st.MobsSpawned++;

            var mobProto = _random.Pick(wave.MobPrototypes);
            var spawned = TrySpawnMob(mobProto, st);
            Sawmill.Debug($"Tried to spawn {mobProto}, result: {spawned}");

            if (attemptsLeft > 0)
                Timer.Spawn(interval, SpawnNext);
            else
                st.AllMobsSpawned = true;
        }

        Sawmill.Debug("Timer SpawnNext");
        Timer.Spawn(TimeSpan.Zero, SpawnNext);
    }

    private bool TrySpawnMob(string prototype, EventState? st = null)
    {
        Sawmill.Debug($"TrySpawnMob called with prototype: {prototype}");

        if (!_proto.HasIndex<EntityPrototype>(prototype))
        {
            Sawmill.Warning($"Missing prototype for Halloween mob: {prototype}");
            return false;
        }

        MapCoordinates spawnCoords;
        if (st?.Portal is { } portal && Exists(portal))
        {
            spawnCoords = _transform.GetMapCoordinates(portal);
            Sawmill.Debug($"Using portal coordinates: {spawnCoords}");
        }
        else
        {
            spawnCoords = GetRandomPortalCoordinates();
            Sawmill.Debug($"Using random coordinates: {spawnCoords}");
        }

        if (spawnCoords == MapCoordinates.Nullspace)
        {
            Sawmill.Warning("Could not find any valid spawn location for a Halloween mob.");
            return false;
        }

        var mob = Spawn(prototype, spawnCoords);
        EnsureComp<HalloweenMobComponent>(mob);
        Sawmill.Debug($"Successfully spawned {prototype} at {spawnCoords}");
        return true;
    }

    private void ScheduleWaveEnd(EntityUid ruleUid, HalloweenRuleComponent comp, EventState st, TimeSpan length)
    {
        if (st.WaveEndScheduled)
            return;

        st.WaveEndScheduled = true;
        var endTime = Timing.CurTime + length;

        void CheckWaveEnd()
        {
            if (!comp.Active || st.WaveCompleted)
                return;

            if (Timing.CurTime >= endTime || (st.AllMobsSpawned && AllHalloweenMobsDead()))
            {
                st.WaveCompleted = true;
                var allDefeated = AllHalloweenMobsDead();
                CleanupHalloweenMobs();

                var endMsg = GetWaveEndAnnouncement(st.CurrentWave, allDefeated);
                _chatSystem.DispatchGlobalAnnouncement(endMsg, "ЦентКом");

                Timer.Spawn(comp.TimeBetweenWaves, () => StartNextWave(ruleUid, comp, st));
            }
            else
            {
                Timer.Spawn(TimeSpan.FromSeconds(5), CheckWaveEnd);
            }
        }

        Timer.Spawn(TimeSpan.FromSeconds(5), CheckWaveEnd);
    }

    private bool AllHalloweenMobsDead()
    {
        var query = EntityQueryEnumerator<HalloweenMobComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState))
        {
            if (!_mobState.IsDead(uid, mobState))
                return false;
        }

        return true;
    }

    private void CleanupHalloweenMobs()
    {
        var query = EntityQueryEnumerator<HalloweenMobComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (Exists(uid))
                QueueDel(uid);
        }
        Sawmill.Info("Cleaned up all Halloween mobs.");
    }

    private void SpawnQueenSequence(EntityUid ruleUid, HalloweenRuleComponent component, EventState st)
    {
        if (!component.Active || component.QueenWave == null)
            return;

        // Delete old portal
        if (st.Portal is { } oldPortal && Exists(oldPortal))
        {
            QueueDel(oldPortal);
            st.Portal = null;
        }

        var brigCoords = GetBrigCoordinates();
        if (brigCoords == MapCoordinates.Nullspace)
        {
            Sawmill.Warning("Could not find Brig for queen spawn, using random location.");
            // Get random coordinates and spawn portal there
            var stations = _station.GetStations();
            foreach (var station in stations)
            {
                if (!TryComp<StationDataComponent>(station, out var stationData))
                    continue;

                if (_station.GetLargestGrid(stationData) is not { } gridUid)
                    continue;

                SpawnPortalOnRandomGridLocation(gridUid, component.PortalPrototype);
                // Find the spawned portal
                var portalQuery = EntityQueryEnumerator<HalloweenPortalComponent>();
                while (portalQuery.MoveNext(out var portalUid, out _))
                {
                    st.Portal = portalUid;
                    break;
                }
                brigCoords = _transform.GetMapCoordinates(gridUid);
                break;
            }
            
            if (brigCoords == MapCoordinates.Nullspace)
            {
                Sawmill.Error("Could not find any location for queen spawn! Aborting.");
                return;
            }
        }
        else
        {
            // Spawn portal in brig
            if (_mapManager.TryFindGridAt(brigCoords, out var brigGrid, out _))
            {
                SpawnPortalOnRandomGridLocation(brigGrid, component.PortalPrototype);
                // Find the spawned portal
                var portalQuery = EntityQueryEnumerator<HalloweenPortalComponent>();
                while (portalQuery.MoveNext(out var portalUid, out _))
                {
                    st.Portal = portalUid;
                    break;
                }
            }
        }

        foreach (var (type, count) in component.QueenWave.Escorts)
        {
            if (!_proto.HasIndex<EntityPrototype>(type))
            {
                Sawmill.Warning($"Missing escort prototype for Queen wave: {type}");
                continue;
            }

            for (var i = 0; i < count; i++)
            {
                var escort = Spawn(type, brigCoords);
                EnsureComp<HalloweenMobComponent>(escort);
            }
        }

        st.Queen = Spawn(component.QueenWave.QueenPrototype, brigCoords);
        EnsureComp<HalloweenMobComponent>(st.Queen.Value);

        var queenArrived = Loc.GetString("halloween-queen-arrival");
        _chatSystem.DispatchGlobalAnnouncement(queenArrived, "ЦентКом");

        Timer.Spawn(component.QueenWave.SurvivalDuration, () => CheckQueenForShuttle(component, st));
    }

    private void CheckQueenForShuttle(HalloweenRuleComponent component, EventState st)
    {
        if (!component.Active || st.Queen is not { } queen || !Exists(queen))
            return;

        if (!_mobState.IsDead(queen))
        {
            Sawmill.Info("Halloween Queen survived! Ending round.");
            _roundEnd.EndRound();
        }
    }

    private void OnHalloweenMobStateChanged(EntityUid uid, HalloweenMobComponent mob, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (_state.Values.Any(s => s.Queen == uid))
        {
            Sawmill.Info("Halloween Queen defeated! Ending round.");
            _roundEnd.EndRound();
            return;
        }

        if (!_random.Prob(0.7f))
            return;

        if (!TryComp<MetaDataComponent>(uid, out var meta) || meta.EntityPrototype == null)
            return;

        var pick = meta.EntityPrototype.ID switch
        {
            "MobHalloweenSmallPumpkin" or "MobHalloweenFlyingPumpkin" or "MobHalloweenCrystalPumpkin" => 
                _random.Prob(0.5f) ? "GiftPumpkinRed" : "HalloweenKnife",
            "MobHalloweenAngryPumpkin" => 
                _random.Prob(0.5f) ? "GiftPumpkinRed" : "HalloweenKnife",
            "MobHalloweenSwordGuardianPumpkin" => 
                _random.Prob(0.5f) ? "GiftPumpkinRed" : "HalloweenSword",
            "MobHalloweenSpearGuardianPumpkin" => 
                _random.Prob(0.5f) ? "GiftPumpkinRed" : "HalloweenSpear",
            "MobHalloweenMinionPumpkin" or "MobHalloweenQueen" => 
                _random.Prob(0.2f) ? "WeaponWandHalloweenFireball" : "GiftPumpkinRed",
            _ => "GiftPumpkinRed",
        };

        if (_proto.HasIndex<EntityPrototype>(pick))
            Spawn(pick, _transform.GetMapCoordinates(uid));
        else
            Sawmill.Warning($"Halloween drop proto missing: {pick}");
    }

    private string GetWaveAnnouncement(int wave, string location, HalloweenRuleComponent component)
    {
        if (wave == component.Waves.Count)
            return Loc.GetString("halloween-final-wave-start", ("wave", wave), ("location", location));

        return Loc.GetString("halloween-wave-start", ("wave", wave), ("location", location));
    }

    private string GetWaveEndAnnouncement(int wave, bool allDefeated)
    {
        return Loc.GetString(
            "halloween-wave-end",
            ("wave", wave),
            ("defeated", allDefeated));
    }

    private void SpawnPortalOnRandomGridLocation(EntityUid grid, string toSpawn)
    {
        if (!TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var xform = Transform(grid);
        var targetCoords = xform.Coordinates;
        var gridBounds = gridComp.LocalAABB;

        for (var i = 0; i < 25; i++)
        {
            var randomX = _random.Next((int)gridBounds.Left, (int)gridBounds.Right);
            var randomY = _random.Next((int)gridBounds.Bottom, (int)gridBounds.Top);

            var tile = new Vector2i(randomX, randomY);

            // Check if tile is valid (not space, not air-blocked)
            if (_atmosphere.IsTileSpace(grid, xform.MapUid, tile) ||
                _atmosphere.IsTileAirBlocked(grid, tile, mapGridComp: gridComp))
            {
                continue;
            }

            // Don't spawn inside solid objects
            var valid = true;
            foreach (var ent in _mapSystem.GetAnchoredEntities(grid, gridComp, tile))
            {
                if (!TryComp<PhysicsComponent>(ent, out var body))
                    continue;
                if (body.BodyType != BodyType.Static ||
                    !body.Hard ||
                    (body.CollisionLayer & (int)CollisionGroup.Impassable) == 0)
                    continue;

                valid = false;
                break;
            }
            if (!valid)
                continue;

            targetCoords = _mapSystem.GridTileToLocal(grid, gridComp, tile);
            break;
        }

        Spawn(toSpawn, targetCoords);
    }

    private MapCoordinates GetRandomPortalCoordinates()
    {
        var stations = _station.GetStations();
        foreach (var station in stations)
        {
            if (!TryComp<StationDataComponent>(station, out var stationData))
                continue;

            if (_station.GetLargestGrid(stationData) is not { } gridUid)
                continue;

            SpawnPortalOnRandomGridLocation(gridUid, "HalloweenPortal");
            return _transform.GetMapCoordinates(gridUid);
        }

        var anyGridQuery = EntityQueryEnumerator<MapGridComponent>();
        while (anyGridQuery.MoveNext(out var gridUid, out _))
        {
            SpawnPortalOnRandomGridLocation(gridUid, "HalloweenPortal");
            return _transform.GetMapCoordinates(gridUid);
        }

        return MapCoordinates.Nullspace;
    }

    private MapCoordinates GetBrigCoordinates()
    {
        var query = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (!TryComp<MetaDataComponent>(uid, out var meta))
                continue;

            var protoId = meta.EntityPrototype?.ID;
            if (protoId != null && protoId.Equals("DefaultStationBeaconBrig", StringComparison.OrdinalIgnoreCase))
                return _transform.ToMapCoordinates(xform.Coordinates);
        }

        return MapCoordinates.Nullspace;
    }

    private string GetPortalLocationString(EventState st)
    {
        var coords = MapCoordinates.Nullspace;
        if (st.Portal is { } portal && Exists(portal))
            coords = _transform.GetMapCoordinates(portal);

        return GetLocationString(coords);
    }

    private string GetLocationString(MapCoordinates coords)
    {
        if (coords == MapCoordinates.Nullspace)
            return Loc.GetString("halloween-location-unknown");

        var beacon = _navMap.GetNearestBeaconString(coords);
        if (string.IsNullOrWhiteSpace(beacon))
            return Loc.GetString("halloween-location-station");

        return ColorTagRegex.Replace(beacon, string.Empty);
    }

}
