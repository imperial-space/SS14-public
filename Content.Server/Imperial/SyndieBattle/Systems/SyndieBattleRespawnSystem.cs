using System.Linq;
using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.SyndieBattle.Components;
using Content.Server.Mind;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Imperial.SyndieBattle.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Imperial.SyndieBattle.Systems;

public sealed class SyndieBattleRespawnSystem : GameRuleSystem<SyndieBattleRespawnComponent>
{
    [Dependency] private readonly MapLoaderSystem _mapLoaderSystem = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookupSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SyndieBattleRuleSystem _ruleSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeNetworkEvent<SyndieBattleRespawnRequestEvent>(OnRespawnRequested);
    }

    protected override void Started(EntityUid uid, SyndieBattleRespawnComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        _mapSystem.CreateMap(out var mapId);
        component.ArenaMap = mapId;
        var options = DeserializationOptions.Default with { InitializeMaps = true };

        if (!_mapLoaderSystem.TryLoadGrid(mapId, component.RespawnMap, out var grid, options))
        {
            Log.Error($"Failed to load SyndieBattle respawn map at {component.RespawnMap}.");
            _mapSystem.DeleteMap(mapId);
            component.ArenaMap = null;
            GameTicker.EndGameRule(uid, gameRule);
            return;
        }

        if (grid is not { } loadedGrid || !FindArenaSpawn(component, loadedGrid.Owner))
        {
            _mapSystem.DeleteMap(mapId);
            component.ArenaMap = null;
            GameTicker.EndGameRule(uid, gameRule);
            return;
        }

        RaiseNetworkEvent(new SyndieBattleModeStatusEvent
        {
            Active = true,
            Music = component.Music,
        });
    }

    protected override void Ended(EntityUid uid, SyndieBattleRespawnComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        component.ArenaSpawns.Clear();
        component.NextArenaSpawnIndex = 0;
        if (component.ArenaMap is { } mapId && _mapSystem.MapExists(mapId))
            _mapSystem.DeleteMap(mapId);
        component.ArenaMap = null;
        component.LastRespawn.Clear();
        RaiseNetworkEvent(new SyndieBattleRespawnUiStateEvent { Visible = false });
        RaiseNetworkEvent(new SyndieBattleModeStatusEvent { Active = false });
    }

    private bool FindArenaSpawn(SyndieBattleRespawnComponent component, EntityUid gridUid)
    {
        component.ArenaSpawns.Clear();
        component.NextArenaSpawnIndex = 0;

        var query = EntityQueryEnumerator<SyndieBattleArenaSpawnComponent, TransformComponent>();
        while (query.MoveNext(out var marker, out _, out var transform))
        {
            if (transform.GridUid != gridUid)
                continue;

            component.ArenaSpawns.Add(marker);
        }

        if (component.ArenaSpawns.Count > 0)
            return true;

        Log.Error($"SyndieBattle respawn map {component.RespawnMap} has no arena spawn markers.");
        return false;
    }

    private bool TryGetNextArenaSpawn(SyndieBattleRespawnComponent component, out EntityUid spawn)
    {
        component.ArenaSpawns.RemoveAll(spawn => Deleted(spawn));
        if (component.ArenaSpawns.Count == 0)
        {
            spawn = default;
            return false;
        }

        for (var i = 0; i < component.ArenaSpawns.Count; i++)
        {
            component.NextArenaSpawnIndex %= component.ArenaSpawns.Count;
            var candidate = component.ArenaSpawns[component.NextArenaSpawnIndex];
            component.NextArenaSpawnIndex = (component.NextArenaSpawnIndex + 1) % component.ArenaSpawns.Count;

            var transform = Transform(candidate);
            if (transform.ParentUid == EntityUid.Invalid)
                continue;

            if (transform.MapID == MapId.Nullspace)
                continue;

            if (_lookupSystem
                .GetEntitiesInRange(candidate, 0.5f, LookupFlags.Dynamic)
                .Any(entity => TryComp<MobStateComponent>(entity, out var state) &&
                    !_mobStateSystem.IsDead(entity, state)))
            {
                continue;
            }

            spawn = candidate;
            return true;
        }

        spawn = default;
        return false;
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!TryGetActiveRule(out _, out var component))
            return;

        SendRespawnState(args.Player, component, false);

        RaiseNetworkEvent(new SyndieBattleModeStatusEvent
        {
            Active = true,
            Music = component.Music,
        }, args.Player);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!TryGetActiveRule(out var ruleUid, out var component))
            return;

        RaiseNetworkEvent(new SyndieBattleModeStatusEvent
        {
            Active = true,
            Music = component.Music,
        }, args.Player);

        if (!CanRespawnFrom(args.Entity) || !HasDeadCombatant(ruleUid, args.Player.UserId))
        {
            SendRespawnState(args.Player, component, false);
            return;
        }

        SendRespawnState(args.Player, component, true);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (!TryComp<SyndieBattleScoreComponent>(args.Target, out var marker) ||
            !TryGetActiveRule(out var ruleUid, out var component) ||
            !TryComp<SyndieBattleRuleComponent>(ruleUid, out var rule) ||
            !rule.PlayerScores.TryGetValue(marker.UserId, out var score) ||
            score.CurrentEntity != args.Target || score.Generation != marker.Generation)
        {
            return;
        }

        if (!_mobStateSystem.IsDead(args.Target))
        {
            if (_playerManager.TryGetSessionById(marker.UserId, out var revivedSession))
                SendRespawnState(revivedSession, component, false);

            return;
        }

        if (HasComp<SyndieBattleArenaCombatantComponent>(args.Target))
        {
            component.LastRespawn[marker.UserId] = Timing.CurTime;
            QueueDel(args.Target);
        }

        if (_playerManager.TryGetSessionById(marker.UserId, out var session))
            SendRespawnState(session, component, true);
    }

    private void OnRespawnRequested(SyndieBattleRespawnRequestEvent message, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!TryGetActiveRule(out var ruleUid, out var component) ||
            !HasDeadCombatant(ruleUid, session.UserId) ||
            session.AttachedEntity is not { } controlled ||
            !CanRespawnFrom(controlled) ||
            !_mindSystem.TryGetMind(session, out var mindId, out var mind))
        {
            return;
        }

        var cooldownRemaining = GetCooldownRemaining(session.UserId, component);
        if (cooldownRemaining > TimeSpan.Zero)
        {
            SendRespawnState(session, component, true);
            return;
        }

        if (!TryGetNextArenaSpawn(component, out var arenaSpawn))
            return;

        var mob = Spawn(component.ArenaMob, Transform(arenaSpawn).Coordinates);
        RemComp<MeleeWeaponComponent>(mob);
        EnsureComp<SyndieBattleArenaCombatantComponent>(mob);
        EnsureComp<MindContainerComponent>(mob);
        if (TryComp<SyndieBattleRuleComponent>(ruleUid, out var rule) &&
            rule.PlayerScores.TryGetValue(session.UserId, out var score))
        {
            var marker = EnsureComp<SyndieBattleScoreComponent>(mob);
            marker.UserId = session.UserId;
            marker.Generation = score.Generation;
            score.CurrentEntity = mob;
            score.Alive = !_mobStateSystem.IsDead(mob);
            score.SpawnTime = Timing.CurTime;
            score.ArenaAttempts++;
            _ruleSystem.UpdateLeader(rule);
        }
        _mindSystem.TransferTo(mindId, mob, ghostCheckOverride: true, mind: mind);

        RaiseNetworkEvent(new SyndieBattleRespawnUiStateEvent { Visible = false }, session);
    }

    private bool HasDeadCombatant(EntityUid ruleUid, NetUserId userId)
    {
        if (!TryComp<SyndieBattleRuleComponent>(ruleUid, out var rule) ||
            !rule.PlayerScores.TryGetValue(userId, out var score) ||
            score.CurrentEntity is not { } combatant)
        {
            return false;
        }

        if (Deleted(combatant))
            return true;

        return TryComp<MobStateComponent>(combatant, out var state) && _mobStateSystem.IsDead(combatant, state);
    }

    private bool CanRespawnFrom(EntityUid controlled)
    {
        if (HasComp<GhostComponent>(controlled))
            return true;

        return TryComp<MobStateComponent>(controlled, out var mobState) && _mobStateSystem.IsDead(controlled, mobState);
    }

    private void SendRespawnState(ICommonSession session, SyndieBattleRespawnComponent component, bool visible)
    {
        RaiseNetworkEvent(
            new SyndieBattleRespawnUiStateEvent
            {
                Visible = visible && component.ArenaSpawns.Any(spawn => !Deleted(spawn)),
                CooldownRemaining = GetCooldownRemaining(session.UserId, component),
            },
            session
        );
    }

    private TimeSpan GetCooldownRemaining(NetUserId userId, SyndieBattleRespawnComponent component)
    {
        if (!component.LastRespawn.TryGetValue(userId, out var lastRespawn))
            return TimeSpan.Zero;

        var remaining = lastRespawn + component.Cooldown - Timing.CurTime;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private bool TryGetActiveRule(out EntityUid ruleUid, out SyndieBattleRespawnComponent component)
    {
        var query = EntityQueryEnumerator<SyndieBattleRespawnComponent, GameRuleComponent>();
        while (query.MoveNext(out ruleUid, out var ruleComponent, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(ruleUid, gameRule))
                continue;

            component = ruleComponent;
            return true;
        }

        ruleUid = default;
        component = default!;
        return false;
    }
}
