using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using System;
using Robust.Shared.Timing;
using Content.Shared.Chat;
using Robust.Shared.Random;
using Robust.Shared.Localization;
using Content.Server.GameTicking.Rules;
using Content.Shared.Mobs;
using Content.Shared.GameTicking.Components;
using Content.Server.Chat.Managers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.GameTicking;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Content.Server.GameTicking.Rules.Components;
using Robust.Server.Player;
using Content.Shared.Imperial.XxRaay.Halloween;
using Content.Server.Station.Systems;
using Content.Server.Station.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Components;

namespace Content.Server.Imperial.XxRaay.Halloween;

public sealed class HalloweenRuleSystem : GameRuleSystem<HalloweenRuleComponent>
{
    [Dependency] private readonly IRobustRandom _rand = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly Content.Server.RoundEnd.RoundEndSystem _roundEnd = default!;

    private EntityUid? _portal;
    private TimeSpan _waveStartTime;
    private int _currentWave = 0;
    private CancellationTokenSource? _waveCts;
    private EntityUid? _queen;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    protected override void Started(EntityUid uid, HalloweenRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        component.Active = true;
        _currentWave = 0;

        MapCoordinates spawnPos = default;
        if (_station.GetStations().Count > 0)
        {
            var station = _station.GetStations()[0];
            if (_station.GetLargestGrid(Comp<StationDataComponent>(station)) is { } grid)
            {
                var playableArea = _physics.GetWorldAABB(grid);
                var center = playableArea.Center;
                var mapId = Transform(grid).MapID;
                spawnPos = new MapCoordinates(center, mapId);
            }
        }

        if (spawnPos != default && _proto.HasIndex("HalloweenPortal"))
        {
            _portal = Spawn("HalloweenPortal", spawnPos);
        }

        var locString = spawnPos.ToString();
        var msgPortal = Loc.GetString("halloween-portal-spawn", ("loc", locString));
        _chat.ChatMessageToAll(ChatChannel.Server, msgPortal, msgPortal, EntityUid.Invalid, false, false);

        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromMinutes(3), () => StartNextWave(component));
    }

    protected override void Ended(EntityUid uid, HalloweenRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        component.Active = false;
        _waveCts?.Cancel();
        if (_portal != null && _entMan.EntityExists(_portal.Value))
            _entMan.DeleteEntity(_portal.Value);
    }

    private bool TryGetRandomStationGrid(out EntityUid gridUid, out MapId mapId, out MapCoordinates coords)
    {
        gridUid = default;
        mapId = default;
        coords = default;
        var stations = _station.GetStations();
        if (stations.Count == 0)
            return false;

        var station = stations[0];
        if (_station.GetLargestGrid(Comp<StationDataComponent>(station)) is not { } grid)
            return false;

        var playableArea = _physics.GetWorldAABB(grid);
        var center = playableArea.Center;
        mapId = Transform(grid).MapID;
        coords = new MapCoordinates(center, mapId);
        gridUid = grid;
        return true;
    }

    private void StartNextWave(HalloweenRuleComponent comp)
    {
        _currentWave++;
        _waveStartTime = Timing.CurTime;
        _waveCts?.Cancel();
        _waveCts = new CancellationTokenSource();

        var playerCount = _playerManager.PlayerCount;
        var baseCounts = new[] { 15, 15, 15, 20, 20, 25, 30, 40 };
        var target = baseCounts[Math.Clamp(_currentWave - 1, 0, baseCounts.Length - 1)];

        var waveLength = TimeSpan.FromMinutes(_currentWave >= 4 ? 7 : 5);

        _ = SpawnWaveRoutine(target, waveLength, _waveCts.Token, comp);
    }

    private async Task SpawnWaveRoutine(int total, TimeSpan length, CancellationToken token, HalloweenRuleComponent comp)
    {
        var interval = length.TotalSeconds / Math.Max(1, total);
        for (var i = 0; i < total; i++)
        {
            if (token.IsCancellationRequested) return;
            var mobProto = PickMobForWave(_currentWave);
            if (_portal != null && _entMan.EntityExists(_portal.Value))
            {
                var portalCoords = Transform(_portal.Value).Coordinates.ToMap(_entMan, _transform);
                Spawn(mobProto, portalCoords);
            }
            await Task.Delay(TimeSpan.FromSeconds(interval), token).ContinueWith(_ => { });
        }

        var endTime = Timing.CurTime + length;
        while (Timing.CurTime < endTime && !token.IsCancellationRequested)
        {
            await Task.Delay(1000, token).ContinueWith(_ => { });
            if (AllHalloweenMobsDead())
                break;
        }

        CleanupHalloweenMobs();

        var defeated = AllHalloweenMobsDead();
        var msgWave = Loc.GetString("halloween-wave-end", ("wave", _currentWave), ("defeated", defeated));
        _chat.ChatMessageToAll(ChatChannel.Server, msgWave, msgWave, EntityUid.Invalid, false, false);

        if (_currentWave < 8)
            Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromMinutes(3), () => StartNextWave(comp));
        else if (_currentWave == 8)
        {
            Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromMinutes(10), SpawnQueenSequence);
        }
    }

    private string PickMobForWave(int wave)
    {
        return wave switch
        {
            1 => "MobHalloweenSmallPumpkin",
            2 => "MobHalloweenSmallPumpkin",
            3 => "MobHalloweenAngryPumpkin",
            4 => "MobHalloweenSwordGuardianPumpkin",
            5 => "MobHalloweenSpearGuardianPumpkin",
            6 => "MobHalloweenMinionPumpkin",
            7 => "MobHalloweenMinionPumpkin",
            _ => "MobHalloweenMinionPumpkin",
        };
    }

    private bool AllHalloweenMobsDead()
    {
        var query = _entMan.EntityQueryEnumerator<MobStateComponent>();
        while (query.MoveNext(out var ent, out var mob))
        {
            if (!ent.IsValid())
                continue;
            if (MetaData(ent).EntityName?.ToLowerInvariant().Contains("pumpkin") == true && !_mobState.IsDead(ent, mob))
                return false;
        }
        return true;
    }

    private void CleanupHalloweenMobs()
    {
        var query = _entMan.EntityQueryEnumerator<MobStateComponent>();
        while (query.MoveNext(out var ent, out var mob))
        {
            if (MetaData(ent).EntityName?.ToLowerInvariant().Contains("pumpkin") == true)
            {
                if (!_mobState.IsDead(ent, mob))
                {
                    _entMan.DeleteEntity(ent);
                }
            }
        }
    }

    private void SpawnQueenSequence()
    {
        if (_portal == null || !_entMan.EntityExists(_portal.Value))
            return;

        var coords = Transform(_portal.Value).Coordinates.ToMap(_entMan, _transform);
        var order = new[]
        {
            "MobHalloweenSmallPumpkin",
            "MobHalloweenSmallPumpkin",
            "MobHalloweenSmallPumpkin",
            "MobHalloweenAngryPumpkin",
            "MobHalloweenAngryPumpkin",
            "MobHalloweenMinionPumpkin",
            "MobHalloweenMinionPumpkin",
            "MobHalloweenSwordGuardianPumpkin",
            "MobHalloweenSpearGuardianPumpkin",
            "MobHalloweenMinionPumpkin",
            "ProjectileHalloweenFireball",
        };
        foreach (var proto in order)
        {
            Spawn(proto, coords);
        }

        if (_proto.HasIndex("MobHalloweenQueen"))
        {
            _queen = Spawn("MobHalloweenQueen", coords);
            var msgQueen = Loc.GetString("halloween-queen-arrival");
            _chat.ChatMessageToAll(ChatChannel.Server, msgQueen, msgQueen, EntityUid.Invalid, false, false);
            Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromMinutes(15), () => CheckQueenForShuttle());
        }
    }

    private void CheckQueenForShuttle()
    {
        if (_queen == null)
            return;

        if (!_entMan.EntityExists(_queen.Value))
            return;

        if (!_mobState.IsDead(_queen.Value))
        {
            _roundEnd.RequestRoundEnd(null, false);
        }
    }

    private void OnMobStateChanged(EntityUid uid, MobStateComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var name = MetaData(uid).EntityName;
        if (name == null)
            return;

        if (!name.ToLowerInvariant().Contains("pumpkin"))
            return;

        if (!_rand.Prob(0.5f))
            return;

        var pool = new[]
        {
            "HalloweenSpear",
            "HalloweenKnife",
            "ClothingOuterHalloweenVest",
            "HalloweenSword",
            "WeaponWandHalloweenFireball",
        };

        var pick = _rand.Pick(pool);

        if (_entMan.EntityExists(uid))
        {
            var coords = Transform(uid).Coordinates.ToMap(_entMan, _transform);
            Spawn(pick, coords);
        }
    }
}
