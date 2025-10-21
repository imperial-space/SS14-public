using System.Linq;
using System.Text.RegularExpressions;
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
using Content.Shared.Pinpointer;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
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

    private static readonly ISawmill Sawmill = Logger.GetSawmill("halloween_rule");

    private static readonly Regex ColorTagRegex =
        new(@"\[/?color(?:=[^\]]+)?\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private sealed class EventState
    {
        public EntityUid? Portal;
        public EntityUid? Queen;
        public int CurrentWave;
        public bool WaveEndScheduled;
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

        var st = new EventState { CurrentWave = 0, WaveEndScheduled = false };
        _state[uid] = st;

        var spawnPos = GetRandomPortalCoordinates();
        if (spawnPos != MapCoordinates.Nullspace)
        {
            st.Portal = Spawn(component.PortalPrototype, spawnPos);
            Sawmill.Info($"Halloween portal spawned at {spawnPos}.");

            var portalLoc = GetLocationString(spawnPos);
            var msgPortal = Loc.GetString("halloween-portal-spawn", ("loc", portalLoc));
            _chatSystem.DispatchGlobalAnnouncement(msgPortal, "ЦентКом");
        }
        else
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

        st.WaveEndScheduled = false;
        st.CurrentWave++;

        if (st.CurrentWave > component.Waves.Count)
        {
            if (component.QueenWave != null)
            {
                Timer.Spawn(component.TimeBeforeQueen, () => SpawnQueenSequence(ruleUid, component, st));
            }
            return;
        }

        var wave = component.Waves[st.CurrentWave - 1];
        var portalLocation = GetPortalLocationString(st);
        var announcement = GetWaveAnnouncement(st.CurrentWave, portalLocation, component);
        _chatSystem.DispatchGlobalAnnouncement(announcement, "ЦентКом");

        SpawnWaveRoutine(ruleUid, wave, component, st);
    }

    private void SpawnWaveRoutine(EntityUid ruleUid, HalloweenWave wave, HalloweenRuleComponent comp, EventState st)
    {
        ScheduleWaveEnd(ruleUid, comp, st, wave.WaveLength);

        if (wave.MobCount <= 0 || !wave.MobPrototypes.Any())
            return;

        var interval = wave.WaveLength / wave.MobCount;
        var attemptsLeft = wave.MobCount;

        void SpawnNext()
        {
            if (!comp.Active || attemptsLeft <= 0)
                return;

            attemptsLeft--;

            var mobProto = _random.Pick(wave.MobPrototypes);
            TrySpawnMob(mobProto, st);

            if (attemptsLeft > 0)
                Timer.Spawn(interval, SpawnNext);
        }

        Timer.Spawn(TimeSpan.Zero, SpawnNext);
    }

    private bool TrySpawnMob(string prototype, EventState? st = null)
    {
        if (!_proto.HasIndex<EntityPrototype>(prototype))
        {
            Sawmill.Warning($"Missing prototype for Halloween mob: {prototype}");
            return false;
        }

        MapCoordinates spawnCoords;
        if (st?.Portal is { } portal && Exists(portal))
            spawnCoords = _transform.GetMapCoordinates(portal);
        else
            spawnCoords = GetRandomPortalCoordinates();

        if (spawnCoords == MapCoordinates.Nullspace)
        {
            Sawmill.Warning("Could not find any valid spawn location for a Halloween mob.");
            return false;
        }

        var mob = Spawn(prototype, spawnCoords);
        EnsureComp<HalloweenMobComponent>(mob);
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
            if (!comp.Active)
                return;

            if (Timing.CurTime >= endTime || AllHalloweenMobsDead())
            {
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

        var brigCoords = GetBrigCoordinates();
        if (brigCoords == MapCoordinates.Nullspace)
        {
            Sawmill.Warning("Could not find Brig for queen spawn, using random location.");
            brigCoords = GetRandomPortalCoordinates();
            if (brigCoords == MapCoordinates.Nullspace)
            {
                Sawmill.Error("Could not find any location for queen spawn! Aborting.");
                return;
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

        if (!_random.Prob(0.5f))
            return;

        if (!TryComp<MetaDataComponent>(uid, out var meta) || meta.EntityPrototype == null)
            return;

        var pick = meta.EntityPrototype.ID switch
        {
            "MobHalloweenSmallPumpkin" or "MobHalloweenFlyingPumpkin" or "MobHalloweenCrystalPumpkin" => "GiftPumpkinRed",
            "MobHalloweenAngryPumpkin" => _random.Prob(0.5f) ? "GiftPumpkinRed" : "HalloweenKnife",
            "MobHalloweenSwordGuardianPumpkin" => "HalloweenSword",
            "MobHalloweenSpearGuardianPumpkin" => "HalloweenSpear",
            "MobHalloweenMinionPumpkin" or "MobHalloweenQueen" => "WeaponWandHalloweenFireball",
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
            return Loc.GetString("halloween-final-wave-announcement", ("location", location));

        return Loc.GetString("halloween-wave-announcement", ("wave", wave), ("location", location));
    }

    private string GetWaveEndAnnouncement(int wave, bool allDefeated)
    {
        return Loc.GetString(
            "halloween-wave-end-announcement",
            ("wave", wave),
            ("result", allDefeated
                ? Loc.GetString("halloween-wave-end-result-victory")
                : Loc.GetString("halloween-wave-end-result-partial")));
    }

    private MapCoordinates GetRandomPortalCoordinates()
    {
        var stations = _station.GetStations();
        foreach (var station in stations)
        {
            if (!TryComp<StationDataComponent>(station, out var stationData))
                continue;

            if (_station.GetLargestGrid(stationData) is not { } gridUid ||
                !TryComp<MapGridComponent>(gridUid, out var gridComp))
                continue;

            var tiles = _mapSystem.GetAllTiles(gridUid, gridComp).ToList();
            if (tiles.Count == 0)
                continue;

            var tile = _random.Pick(tiles);
            return _transform.ToMapCoordinates(new EntityCoordinates(gridUid, tile.GridIndices));
        }

        var anyGridQuery = EntityQueryEnumerator<MapGridComponent>();
        while (anyGridQuery.MoveNext(out var gridUid, out var gridComp))
        {
            var tiles = _mapSystem.GetAllTiles(gridUid, gridComp).ToList();
            if (tiles.Count == 0)
                continue;

            var tile = _random.Pick(tiles);
            return _transform.ToMapCoordinates(new EntityCoordinates(gridUid, tile.GridIndices));
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
