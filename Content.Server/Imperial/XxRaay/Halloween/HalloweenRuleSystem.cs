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
using System.Linq;

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

    private EntityUid? _portal;
    private int _currentWave = 0;
    private EntityUid? _queen;

    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void Started(EntityUid uid, HalloweenRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        Sawmill.Info("Halloween rule started.");
        component.Active = true;
        _currentWave = 0;

        SubscribeLocalEvent<HalloweenMobComponent, MobStateChangedEvent>(OnHalloweenMobStateChanged);

        var spawnPos = GetRandomPortalCoordinates();
        if (spawnPos != MapCoordinates.Nullspace)
        {
            _portal = Spawn(component.PortalPrototype, spawnPos);
            Sawmill.Info($"Halloween portal spawned at {spawnPos}.");

            var portalLoc = GetLocationString(spawnPos);
            var msgPortal = Loc.GetString("halloween-portal-spawn", ("loc", portalLoc));
            _chatSystem.DispatchGlobalAnnouncement(msgPortal, "ЦентКом", colorOverride: Color.OrangeRed);
        }
        else
        {
            Sawmill.Warning("Failed to find a valid position to spawn the Halloween portal.");
        }

        Timer.Spawn(component.TimeBetweenWaves, () => StartNextWave(uid, component));
    }

    protected override void Ended(EntityUid uid, HalloweenRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        Sawmill.Info("Halloween rule ended.");
        component.Active = false;

        UnsubscribeLocalEvent<HalloweenMobComponent, MobStateChangedEvent>(OnHalloweenMobStateChanged);

        if (_portal is { } portal && Exists(portal))
            QueueDel(portal);

        CleanupHalloweenMobs();
        _portal = null;
        _queen = null;
        _currentWave = 0;
    }

    private void StartNextWave(EntityUid ruleUid, HalloweenRuleComponent component)
    {
        if (!component.Active)
            return;

        _currentWave++;

        if (_currentWave > component.Waves.Count)
        {
            if (component.QueenWave != null)
            {
                Timer.Spawn(component.TimeBeforeQueen, () => SpawnQueenSequence(ruleUid, component));
            }
            return;
        }

        var waveDef = component.Waves[_currentWave - 1];
        var portalLocation = GetPortalLocationString();
        var announcement = GetWaveAnnouncement(_currentWave, portalLocation, component);
        _chatSystem.DispatchGlobalAnnouncement(announcement, "ЦентКом", colorOverride: Color.OrangeRed);

        SpawnWaveRoutine(waveDef, component);
    }

    private void SpawnWaveRoutine(HalloweenWave wave, HalloweenRuleComponent comp)
    {
        if (wave.MobCount <= 0 || !wave.MobPrototypes.Any())
            return;

        var interval = wave.WaveLength / wave.MobCount;
        var spawned = 0;

        Timer.Spawn(interval, SpawnNextMob);

        void SpawnNextMob()
        {
            if (!comp.Active || spawned >= wave.MobCount)
            {
                MonitorWaveEnd(wave.WaveLength, comp);
                return;
            }

            var mobProto = _random.Pick(wave.MobPrototypes);
            if (TrySpawnMob(mobProto))
                spawned++;

            Timer.Spawn(interval, SpawnNextMob);
        }
    }

    private bool TrySpawnMob(string prototype)
    {
        if (!_proto.HasIndex<EntityPrototype>(prototype))
        {
            Sawmill.Warning($"Missing prototype for Halloween mob: {prototype}");
            return false;
        }

        var spawnCoords = MapCoordinates.Nullspace;
        if (_portal is {} portal && Exists(portal))
            spawnCoords = _transform.GetMapCoordinates(portal);
        else
            spawnCoords = GetRandomPortalCoordinates();

        if (spawnCoords == MapCoordinates.Nullspace)
        {
            Sawmill.Warning("Could not find any valid spawn location for Halloween mob.");
            return false;
        }

        Spawn(prototype, spawnCoords);
        return true;
    }

    private void MonitorWaveEnd(TimeSpan length, HalloweenRuleComponent comp)
    {
        var endTime = Timing.CurTime + length;

        void CheckWaveEnd()
        {
            if (!comp.Active)
                return;

            if (Timing.CurTime >= endTime || AllHalloweenMobsDead())
            {
                var allDefeated = AllHalloweenMobsDead();
                CleanupHalloweenMobs();

                var endMsg = GetWaveEndAnnouncement(_currentWave, allDefeated);
                _chatSystem.DispatchGlobalAnnouncement(endMsg, "ЦентКом", colorOverride: Color.OrangeRed);

                var query = EntityQueryEnumerator<HalloweenRuleComponent, GameRuleComponent>();
                while (query.MoveNext(out var uid, out var h, out _))
                {
                    if (h == comp)
                    {
                        Timer.Spawn(comp.TimeBetweenWaves, () => StartNextWave(uid, comp));
                        return;
                    }
                }
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

    private void SpawnQueenSequence(EntityUid ruleUid, HalloweenRuleComponent component)
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
                Spawn(type, brigCoords);
            }
        }

        _queen = Spawn(component.QueenWave.QueenPrototype, brigCoords);
        var queenArrived = Loc.GetString("halloween-queen-arrival");
        _chatSystem.DispatchGlobalAnnouncement(queenArrived, "ЦентКом", colorOverride: Color.Red);

        Timer.Spawn(component.QueenWave.SurvivalDuration, () => CheckQueenForShuttle(component));
    }

    private void CheckQueenForShuttle(HalloweenRuleComponent component)
    {
        if (!component.Active || _queen is not { } queen || !Exists(queen))
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

        if (_queen == uid)
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
        {
            Spawn(pick, _transform.GetMapCoordinates(uid));
        }
        else
        {
            Sawmill.Warning($"Halloween drop proto missing: {pick}");
        }
    }

    private string GetWaveAnnouncement(int wave, string location, HalloweenRuleComponent component)
    {
        if (wave == component.Waves.Count)
            return Loc.GetString("halloween-final-wave-announcement", ("location", location));

        return Loc.GetString("halloween-wave-announcement", ("wave", wave), ("location", location));
    }

    private string GetWaveEndAnnouncement(int wave, bool allDefeated)
    {
        return Loc.GetString("halloween-wave-end-announcement",
            ("wave", wave),
            ("result", allDefeated
                ? Loc.GetString("halloween-wave-end-result-victory")
                : Loc.GetString("halloween-wave-end-result-partial")));
    }

    private MapCoordinates GetRandomPortalCoordinates()
    {
        var stations = _station.GetStations();
        if (!stations.Any())
            return MapCoordinates.Nullspace;

        if (TryComp<StationDataComponent>(stations[0], out var stationData) &&
            _station.GetLargestGrid(stationData) is { } gridUid &&
            TryComp<MapGridComponent>(gridUid, out var gridComp))
        {
            var tiles = _mapSystem.GetAllTiles(gridUid, gridComp).ToList();
            if (tiles.Any())
            {
                var tile = _random.Pick(tiles);
                return _transform.ToMapCoordinates(new EntityCoordinates(gridUid, tile.GridIndices));
            }
        }

        return MapCoordinates.Nullspace;
    }

    private MapCoordinates GetBrigCoordinates()
    {
        var query = AllEntityQuery<NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beacon, out var xform))
        {
            if (beacon.Text != null && beacon.Text.Equals("Brig", StringComparison.OrdinalIgnoreCase))
            {
                return _transform.GetMapCoordinates(uid, xform);
            }
        }
        return MapCoordinates.Nullspace;
    }

    private string GetPortalLocationString()
    {
        var coords = MapCoordinates.Nullspace;
        if (_portal is {} portal && Exists(portal))
            coords = _transform.GetMapCoordinates(portal);

        return GetLocationString(coords);
    }

    private string GetLocationString(MapCoordinates coords)
    {
        if (coords == MapCoordinates.Nullspace)
            return Loc.GetString("halloween-location-unknown");

        var beacon = _navMap.GetNearestBeaconString(coords);
        return string.IsNullOrWhiteSpace(beacon)
            ? Loc.GetString("halloween-location-station")
            : beacon;
    }
}