using System.Linq;
using Content.Server.Imperial.SyndieBattle.Data;
using Content.Shared.Inventory;
using Content.Shared.Hands.Components;
using Content.Shared.Store.Components;
using Robust.Shared.Random;
using Content.Server.Station.Components;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Player;
using Content.Shared.Mind;
using Content.Shared.Imperial.CrewSkills;
using Content.Server.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Clothing.Systems;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Roles;
using Content.Shared.Chat;
using Content.Shared.FixedPoint;
using Content.Shared.Mind.Components;
using Content.Server.KillTracking;
using Content.Server.Pinpointer;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared.Preferences;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Content.Server.Station.Systems;
using Content.Server.Store.Systems;
using Content.Shared.Imperial.SyndieBattle.Components;
using Robust.Shared.Map;
using Content.Server.Hands.Systems;
using Content.Server.Imperial.SyndieBattle.Components;
using Content.Server.Imperial.SyndieBattle.Systems;
using Content.Shared.Station.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Network;
using Content.Server.RoundEnd;
using Content.Server.Imperial.SyndieBattle.Types;

namespace Content.Server.Imperial.SyndieBattle;

public sealed partial class SyndieBattleRuleSystem : GameRuleSystem<SyndieBattleRuleComponent>
{
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly ObjectivesSystem _objectivesSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly SharedCrewSkillsSystem _crewSkillsSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly NavMapSystem _navMapSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly OutfitSystem _outfitSystem = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly SyndieBattleSpawnProtectionSystem _spawnProtectionSystem = default!;
    [Dependency] private readonly IServerPreferencesManager _preferencesManager = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly StoreSystem _storeSystem = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<SyndieBattleScoreComponent, MobStateChangedEvent>(OnCombatantMobStateChanged, after: [typeof(KillTrackingSystem)]);
        SubscribeLocalEvent<StoreBuyFinishedEvent>(OnStoreBuyFinished);
    }

    protected override void Started(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        component.Active = false;
        component.ActivationPending = true;
        component.PlayerScores.Clear();
        component.RedemptionMachines.Clear();
        component.PendingGoalAnnouncements.Clear();
        component.FirstDeathName = null;
        component.FirstDeathTime = null;
        component.Leader = null;
        component.Winner = null;
        component.WinnerName = null;
        component.WinnerKills = 0;
        component.RoundEndReason = null;
        component.RoundEndsAt = null;
        component.PendingRoundTimeAnnouncements.Clear();
        component.RecentKills.Clear();
        component.NextParticipationOrder = 0;
    }

    private void ActivateMode(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule)
    {
        if (Deleted(uid) ||
            !GameTicker.IsGameRuleActive(uid, gameRule) ||
            !TryComp<SyndieBattleRespawnComponent>(uid, out var respawn) ||
            !respawn.ArenaSpawns.Any(arenaSpawn => !Deleted(arenaSpawn)))
        {
            return;
        }

        component.Active = true;
        component.ActivationPending = false;
        component.RoundEndsAt = Timing.CurTime + component.RoundDuration;
        component.PendingRoundTimeAnnouncements = new Queue<TimeSpan>(
            component.RoundTimeAnnouncements.Where(time => time > TimeSpan.Zero && time < component.RoundDuration).OrderByDescending(time => time)
        );
        SpawnRedemptionMachines();

        var goal = GetGoalAnnouncement(component);
        _chatManager.ChatMessageToAll(ChatChannel.Server, goal, goal, default, false, false);

        var rules = Loc.GetString(component.RulesAnnouncement, ("seconds", (int)Math.Ceiling(component.SpawnProtectionDuration.TotalSeconds)));
        _chatManager.ChatMessageToAll(ChatChannel.Server, rules, rules, default, false, false);

        var guide = GetGuideAnnouncement(component);
        _chatManager.ChatMessageToAll(ChatChannel.Server, guide, guide, default, false, false);

        ConvertAllCurrentPlayers(uid, component);
    }

    protected override void Ended(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        component.Active = false;
        component.ActivationPending = false;
        ClearLeader(component);
        foreach (var machine in component.RedemptionMachines)
        {
            QueueDel(machine);
        }

        component.RedemptionMachines.Clear();
        component.PendingGoalAnnouncements.Clear();
        component.PendingRoundTimeAnnouncements.Clear();
    }

    protected override void ActiveTick(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        if (component.ActivationPending)
            ActivateMode(uid, component, gameRule);

        if (!component.Active)
            return;

        if (component.Winner == null &&
            component.RoundEndsAt is { } roundEndsAt &&
            Timing.CurTime >= roundEndsAt)
        {
            FinishByTimeLimit(component);
            return;
        }

        AnnounceRoundTime(component);

        for (var i = component.PendingGoalAnnouncements.Count - 1; i >= 0; i--)
        {
            var pending = component.PendingGoalAnnouncements[i];
            if (Timing.CurTime < pending.At)
                continue;

            component.PendingGoalAnnouncements.RemoveAt(i);
            if (!_playerManager.TryGetSessionById(pending.UserId, out var session))
                continue;

            SendModeGuide(component, session.Channel);
        }
    }

    protected override void AppendRoundEndText(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        var players = new List<SyndieBattleRoundEndPlayerData>();
        var now = Timing.CurTime;

        foreach (var score in component.PlayerScores.Values)
        {
            var player = score.CurrentEntity;
            var tc = 0;
            if (TryGetPersistentStore(score, out var store))
                tc = GetBalance(store.Comp, component);

            var duration = score.SurvivalTime;
            if (score.Alive && player is { } body && !HasComp<SyndieBattleArenaCombatantComponent>(body))
                duration += now - score.SpawnTime;

            var alive = score.Alive && player is { } current && TryComp<MobStateComponent>(current, out var mobState) && !_mobStateSystem.IsDead(current, mobState);

            players.Add(
                new SyndieBattleRoundEndPlayerData
                {
                    Name = score.Name,
                    Kills = score.KillCount,
                    Deaths = score.Deaths,
                    TC = tc,
                    SpentTc = score.SpentTelecrystals,
                    StolenTc = score.StolenTelecrystals,
                    Duration = duration,
                    Alive = alive,
                    ArenaAttempts = score.ArenaAttempts,
                    ArenaCompletions = score.ArenaCompletions,
                    RedeemedPdas = score.RedeemedPdas,
                    RedeemedTc = score.RedeemedTelecrystals,
                    BestStreak = score.BestKillStreak,
                    ComebackTc = score.ComebackTelecrystals,
                }
            );
        }

        args.AddLine(Loc.GetString("syndiebattle-round-end-title") + "\n");
        if (component.WinnerName is { } winnerName && component.RoundEndReason is { } reason)
        {
            args.AddLine(
                Loc.GetString(
                    reason == SyndieBattleRoundEndReason.KillLimit ? "syndiebattle-round-end-winner-kills" : "syndiebattle-round-end-winner-time",
                    ("name", winnerName),
                    ("kills", component.WinnerKills)
                ) + "\n"
            );
        }
        args.AddLine(Loc.GetString("syndiebattle-rating-header"));
        var rating = players.OrderByDescending(player => player.Kills).ThenByDescending(player => player.StolenTc).ThenByDescending(player => player.TC).Take(component.ManifestEntryLimit);
        var rank = 1;
        foreach (var player in rating)
        {
            args.AddLine(
                Loc.GetString(
                    "syndiebattle-rating-entry",
                    ("rank", rank++),
                    ("name", player.Name),
                    ("kills", player.Kills),
                    ("deaths", player.Deaths),
                    ("tc", player.TC),
                    ("spentTc", player.SpentTc),
                    ("stolenTc", player.StolenTc),
                    ("time", player.Duration.ToString(@"hh\:mm\:ss")),
                    ("status", Loc.GetString(player.Alive ? "syndiebattle-survive-status-alive" : "syndiebattle-survive-status-dead")),
                    ("arenaCompletions", player.ArenaCompletions),
                    ("arenaAttempts", player.ArenaAttempts),
                    ("redeemedPdas", player.RedeemedPdas),
                    ("redeemedTc", player.RedeemedTc)
                )
            );
        }

        args.AddLine("\n" + Loc.GetString("syndiebattle-awards-header"));
        if (component.FirstDeathName is { } firstDeathName && component.FirstDeathTime is { } firstDeathTime)
        {
            args.AddLine(Loc.GetString("syndiebattle-award-first-death", ("name", firstDeathName), ("time", firstDeathTime.ToString(@"hh\:mm\:ss"))));
        }

        var spender = players.OrderByDescending(player => player.SpentTc).FirstOrDefault();
        if (spender.SpentTc > 0)
            args.AddLine(Loc.GetString("syndiebattle-award-spender", ("name", spender.Name), ("tc", spender.SpentTc)));

        var thief = players.OrderByDescending(player => player.StolenTc).FirstOrDefault();
        if (thief.StolenTc > 0)
            args.AddLine(Loc.GetString("syndiebattle-award-thief", ("name", thief.Name), ("tc", thief.StolenTc)));

        var survivor = players.OrderByDescending(player => player.Duration).FirstOrDefault();
        if (!string.IsNullOrEmpty(survivor.Name))
        {
            args.AddLine(Loc.GetString("syndiebattle-award-survivor", ("name", survivor.Name), ("time", survivor.Duration.ToString(@"hh\:mm\:ss"))));
        }

        var arenaChampion = players.Where(player => player.ArenaAttempts > 0).OrderByDescending(player => player.ArenaCompletions).ThenBy(player => player.ArenaAttempts).FirstOrDefault();
        if (arenaChampion.ArenaAttempts > 0)
        {
            args.AddLine(Loc.GetString("syndiebattle-manifest-arena-award", ("name", arenaChampion.Name), ("completions", arenaChampion.ArenaCompletions), ("attempts", arenaChampion.ArenaAttempts)));
        }

        var recycler = players.OrderByDescending(player => player.RedeemedPdas).FirstOrDefault();
        if (recycler.RedeemedPdas > 0)
        {
            args.AddLine(Loc.GetString("syndiebattle-manifest-redemption-award", ("name", recycler.Name), ("pdas", recycler.RedeemedPdas), ("tc", recycler.RedeemedTc)));
        }

        var streak = players.OrderByDescending(player => player.BestStreak).FirstOrDefault();
        if (streak.BestStreak > 1)
        {
            args.AddLine(Loc.GetString("syndiebattle-award-streak", ("name", streak.Name), ("kills", streak.BestStreak)));
        }

        var comeback = players.OrderByDescending(player => player.ComebackTc).FirstOrDefault();
        if (comeback.ComebackTc > 0)
        {
            args.AddLine(Loc.GetString("syndiebattle-award-comeback", ("name", comeback.Name), ("tc", comeback.ComebackTc)));
        }

        var efficient = players
            .Where(player => player.Kills > 0)
            .OrderByDescending(player => (double) player.Kills / Math.Max(1, player.SpentTc))
            .ThenByDescending(player => player.Kills)
            .FirstOrDefault();
        if (efficient.Kills > 0)
        {
            args.AddLine(Loc.GetString("syndiebattle-award-efficient", ("name", efficient.Name), ("kills", efficient.Kills), ("tc", efficient.SpentTc)));
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!TryGetActiveRuleEntity(out var activeRule) || !TryComp<SyndieBattleRuleComponent>(activeRule, out var component))
            return;

        MakeTraitor(activeRule, ev.Mob, component, ev.Profile, ev.Station);

        var userId = ev.Player.UserId;
        var delay = component.GoalMessageDelay + component.GoalMessageJitter * _random.NextDouble();
        component.PendingGoalAnnouncements.Add((userId, Timing.CurTime + delay));
    }

    public void PrepareRespawnedCombatant(EntityUid player, SyndieBattleRuleComponent component, HumanoidCharacterProfile profile, EntityUid station)
    {
        if (!TryGetActiveRuleEntity(out var activeRule))
            return;

        MakeTraitor(activeRule, player, component, profile, station);
    }

    private void MakeTraitor(EntityUid ruleUid, EntityUid player, SyndieBattleRuleComponent component, HumanoidCharacterProfile? profile = null, EntityUid? station = null)
    {
        station ??= _stationSystem.GetOwningStation(player);
        if (TryComp<InventoryComponent>(player, out var inv))
        {
            var toDelete = new List<EntityUid>();
            var enumSlots = _inventorySystem.GetSlotEnumerator((player, inv));
            while (enumSlots.NextItem(out var item, out _))
            {
                if (item is EntityUid itemUid)
                    toDelete.Add(itemUid);
            }

            if (TryComp<HandsComponent>(player, out var hands))
            {
                foreach (var (handId, _) in hands.Hands)
                {
                    if (_handsSystem.TryGetHeldItem(player, handId, out var helded))
                        toDelete.Add(helded.Value);
                }
            }

            foreach (var ent in toDelete.Distinct())
            {
                if (ent == default || EntityManager.Deleted(ent))
                    continue;
                QueueDel(ent);
            }
        }

        _outfitSystem.SetOutfit(player, component.StartingGear);

        EnsureComp<KillTrackerComponent>(player);

        if (_mindSystem.TryGetMind(player, out var mindId, out var mind))
        {
            if (!_roleSystem.MindHasRole<SyndieBattleRoleComponent>(mindId))
            {
                _roleSystem.MindAddRole(mindId, component.MindRole, mind);
                AssignTraitorObjectives(player, component);
            }

            EnsureComp<MindSkillsComponent>(mindId);
            _crewSkillsSystem.AddGroupToMindAttempt(mindId, component.SkillGroup);

            if (_playerManager.TryGetSessionByEntity(player, out var session))
            {
                var userId = session.UserId;
                var scoreMarker = EnsureComp<SyndieBattleScoreComponent>(player);
                scoreMarker.UserId = userId;

                if (!component.PlayerScores.TryGetValue(userId, out var score))
                {
                    score = new SyndieBattlePlayerScore
                    {
                        ParticipationOrder = component.NextParticipationOrder++,
                    };
                    component.PlayerScores.Add(userId, score);
                }

                score.Name = MetaData(player).EntityName;
                score.Profile = profile ?? score.Profile;
                score.Station = station ?? score.Station;
                if (score.Station is { } owningStation &&
                    TryComp<SyndieBattlePowerComponent>(ruleUid, out var power))
                {
                    power.Stations.Add(owningStation);
                }
                if (!score.Alive || score.CurrentEntity != player)
                    score.SpawnTime = Timing.CurTime;
                if (component.Leader == userId && score.CurrentEntity is { } oldEntity && oldEntity != player)
                    RemCompDeferred<SyndieBattleLeaderComponent>(oldEntity);
                score.Alive = true;
                score.CurrentEntity = player;
                if (component.Leader == userId)
                    SetLeaderIcon(player, component);
                score.Generation++;
                scoreMarker.Generation = score.Generation;
                if (TryGetCombatantStore(player, out var store))
                {
                    score.StoreEntity = store.Owner;
                    var pdaOwner = EnsureComp<SyndieBattlePdaOwnerComponent>(store.Owner);
                    pdaOwner.UserId = userId;
                    pdaOwner.Generation = score.Generation;
                    pdaOwner.RedemptionAllowed = true;
                    var persistent = EnsurePersistentStore(mindId, component, score);
                    CopyPersistentBalanceToStore(persistent, store);
                }
            }
        }

        _spawnProtectionSystem.Apply(player, component.SpawnProtectionDuration, component.SpawnProtectionStartMessage, component.SpawnProtectionEndMessage);

        ScatterCombatant(player, station, component);
        UpdateLeader(component);
    }

    private void AssignTraitorObjectives(EntityUid player, SyndieBattleRuleComponent component)
    {
        if (!_mindSystem.TryGetMind(player, out var mindId, out var mind))
            return;

        _mindSystem.TryAddObjective(mindId, mind, component.PrimaryObjective);

        var maxDifficulty = component.MaxObjectiveDifficulty;
        var picked = 0;
        while (picked < component.AdditionalObjectiveCount && maxDifficulty > 0f)
        {
            var objective = _objectivesSystem.GetRandomObjective(mindId, mind, component.AdditionalObjectiveGroup, maxDifficulty);
            if (objective is null)
                break;

            _mindSystem.AddObjective(mindId, mind, objective.Value);
            var diff = Comp<ObjectiveComponent>(objective.Value).Difficulty;
            maxDifficulty -= diff;
            picked++;
        }
    }

    private void OnStoreBuyFinished(ref StoreBuyFinishedEvent ev)
    {
        if (!HasComp<SyndieBattlePdaComponent>(ev.StoreUid) ||
            !TryComp<SyndieBattlePdaOwnerComponent>(ev.StoreUid, out var owner) ||
            !TryGetActiveRuleEntity(out var ruleUid) ||
            !TryComp<SyndieBattleRuleComponent>(ruleUid, out var rule) ||
            !rule.PlayerScores.TryGetValue(owner.UserId, out var score) ||
            score.StoreEntity != ev.StoreUid || score.Generation != owner.Generation ||
            !ev.PurchasedItem.Cost.TryGetValue(rule.Currency, out var spent))
        {
            return;
        }

        score.SpentTelecrystals += (int) spent;
        if (TryGetPersistentStore(score, out var persistent))
            CopyStoreBalanceToPersistent((ev.StoreUid, Comp<StoreComponent>(ev.StoreUid)), persistent);
    }

    /// <summary>
    /// Only the current combatant may use the PDA issued for their current life.
    /// </summary>
    public bool CanUseStore(Entity<SyndieBattlePdaOwnerComponent> store, EntityUid user)
    {
        return _playerManager.TryGetSessionByEntity(user, out var session) &&
               session.UserId == store.Comp.UserId &&
               TryGetActiveRuleEntity(out var ruleUid) &&
               TryComp<SyndieBattleRuleComponent>(ruleUid, out var rule) &&
               rule.PlayerScores.TryGetValue(session.UserId, out var score) &&
               score.CurrentEntity == user &&
               score.StoreEntity == store.Owner &&
               score.Generation == store.Comp.Generation;
    }

    private void OnCombatantMobStateChanged(EntityUid uid, SyndieBattleScoreComponent component, ref MobStateChangedEvent args)
    {
        if (!TryGetActiveRuleEntity(out var activeRule) ||
            !TryComp<SyndieBattleRuleComponent>(activeRule, out var rule) ||
            !rule.PlayerScores.TryGetValue(component.UserId, out var victimScore))
        {
            return;
        }

        if (component.Generation != victimScore.Generation ||
            victimScore.CurrentEntity != uid ||
            !TryComp<MobStateComponent>(uid, out var mobState))
            return;

        if (HasComp<SyndieBattleArenaCombatantComponent>(uid))
        {
            victimScore.Alive = !_mobStateSystem.IsDead(uid, mobState);
            return;
        }

        if (!_mobStateSystem.IsDead(uid, mobState))
        {
            if (!victimScore.Alive)
            {
                victimScore.SpawnTime = Timing.CurTime;
                if (rule.Leader == component.UserId)
                    SetLeaderIcon(uid, rule);
            }
            victimScore.Alive = true;
            UpdateLeader(rule);
            return;
        }

        if (!victimScore.Alive)
            return;

        victimScore.SurvivalTime += Timing.CurTime - victimScore.SpawnTime;
        victimScore.Alive = false;
        if (rule.Leader == component.UserId)
            RemCompDeferred<SyndieBattleLeaderComponent>(uid);
        victimScore.Deaths++;
        victimScore.CurrentKillStreak = 0;
        var victimName = MetaData(uid).EntityName;
        if (rule.FirstDeathName == null)
        {
            rule.FirstDeathName = victimName;
            rule.FirstDeathTime = GameTicker.RoundDuration();
        }

        var killer = GetDeathSource(args.Origin, (uid, component));
        var suicide = killer is KillPlayerSource player && player.PlayerId == component.UserId;
        SyndieBattlePlayerScore? creditedKiller = null;
        NetUserId creditedUser = default;
        if (!suicide &&
            killer is KillPlayerSource playerSource &&
            rule.PlayerScores.TryGetValue(playerSource.PlayerId, out var killerScore))
        {
            if (IsCreditedKill(rule, playerSource.PlayerId, component.UserId))
            {
                killerScore.KillCount++;
                killerScore.CurrentKillStreak++;
                killerScore.BestKillStreak = Math.Max(killerScore.BestKillStreak, killerScore.CurrentKillStreak);
                var killedLeader = rule.Leader == component.UserId;
                var transfer = TransferTelecrystals(victimScore, killerScore, rule, killedLeader ? rule.LeaderKillReward : 0);
                killerScore.StolenTelecrystals += transfer.VictimLoss;
                AnnounceKillStreak(rule, killerScore);
                creditedKiller = killerScore;
                creditedUser = playerSource.PlayerId;
            }
            else
            {
                SetPdaRedemptionAllowed(victimScore, false);
                SendAntiFarmMessage(playerSource.PlayerId, rule.RepeatKillCooldown);
            }
        }

        UpdateLeader(rule);
        if (creditedKiller != null)
            FinishByKillLimit(rule, creditedUser, creditedKiller);

        var killerName = GetKillerName(killer);
        var location = GetDeathLocation(uid);

        var message = Loc.GetString("syndiebattle-kill-detail", ("killer", killerName), ("victim", victimName), ("location", location));

        _chatManager.ChatMessageToAll(ChatChannel.Server, message, message, uid, false, false);
    }

    private bool TryGetPlayerStore(SyndieBattlePlayerScore score, out Entity<StoreComponent> store)
    {
        store = default;
        if (score.StoreEntity is not { } storeUid ||
            Deleted(storeUid) ||
            !HasComp<SyndieBattlePdaComponent>(storeUid) ||
            !TryComp<StoreComponent>(storeUid, out var component))
        {
            return false;
        }

        store = (storeUid, component);
        return true;
    }

    private bool TryGetCombatantStore(EntityUid? combatant, out Entity<StoreComponent> store)
    {
        store = default;
        if (combatant is not { } owner || Deleted(owner))
            return false;

        if (TryComp<InventoryComponent>(owner, out var inventory))
        {
            var slots = _inventorySystem.GetSlotEnumerator((owner, inventory));
            while (slots.NextItem(out var item, out _))
            {
                if (item is { } itemUid &&
                    HasComp<SyndieBattlePdaComponent>(itemUid) &&
                    TryComp<StoreComponent>(itemUid, out var component))
                {
                    store = (itemUid, component);
                    return true;
                }
            }
        }

        if (!TryComp<HandsComponent>(owner, out var hands))
            return false;

        foreach (var handId in hands.Hands.Keys)
        {
            if (!_handsSystem.TryGetHeldItem(owner, handId, out var item))
                continue;

            if (!HasComp<SyndieBattlePdaComponent>(item.Value) || !TryComp<StoreComponent>(item.Value, out var component))
            {
                continue;
            }

            store = (item.Value, component);
            return true;
        }

        return false;
    }

    private KillSource GetDeathSource(EntityUid? origin, Entity<SyndieBattleScoreComponent> victim)
    {
        if (origin == victim.Owner)
            return new KillPlayerSource(victim.Comp.UserId);

        if (TryComp<ActorComponent>(origin, out var actor))
            return new KillPlayerSource(actor.PlayerSession.UserId);

        if (!TryComp<KillTrackerComponent>(victim, out var tracker))
            return new KillEnvironmentSource();

        KillSource? largestSource = null;
        var largestDamage = FixedPoint2.Zero;
        foreach (var (source, damage) in tracker.LifetimeDamage)
        {
            if (damage < largestDamage)
                continue;

            largestSource = source;
            largestDamage = damage;
        }

        return largestSource ?? new KillEnvironmentSource();
    }

    private bool TryGetActiveRuleEntity(out EntityUid ruleUid)
    {
        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (!comp.Active)
                continue;

            ruleUid = uid;
            return true;
        }

        ruleUid = default;
        return false;
    }

    /// <summary>
    /// Конвертирует всех текущих игроков в предателей
    /// </summary>
    private void ConvertAllCurrentPlayers(EntityUid ruleUid, SyndieBattleRuleComponent component)
    {
        var query = EntityQueryEnumerator<ActorComponent, MindContainerComponent, MobStateComponent>();

        while (query.MoveNext(out var uid, out var actor, out _, out var mobState))
        {
            if (_mobStateSystem.IsDead(uid, mobState))
                continue;

            var profile = _preferencesManager.GetPreferences(actor.PlayerSession.UserId).SelectedCharacter as HumanoidCharacterProfile;
            var station = _stationSystem.GetOwningStation(uid);
            MakeTraitor(ruleUid, uid, component, profile, station);
        }
    }

    private void SendModeGuide(SyndieBattleRuleComponent component, INetChannel channel)
    {
        var goal = GetGoalAnnouncement(component);
        var rules = Loc.GetString(component.RulesAnnouncement, ("seconds", (int)Math.Ceiling(component.SpawnProtectionDuration.TotalSeconds)));
        var guide = GetGuideAnnouncement(component);

        _chatManager.ChatMessageToOne(ChatChannel.Server, goal, goal, EntityUid.Invalid, false, channel);
        _chatManager.ChatMessageToOne(ChatChannel.Server, rules, rules, EntityUid.Invalid, false, channel);
        _chatManager.ChatMessageToOne(ChatChannel.Server, guide, guide, EntityUid.Invalid, false, channel);
    }

    private string GetGoalAnnouncement(SyndieBattleRuleComponent component)
    {
        return Loc.GetString(component.GoalAnnouncement, ("killLimit", component.VictoryKillLimit), ("minutes", (int) component.RoundDuration.TotalMinutes));
    }

    private string GetGuideAnnouncement(SyndieBattleRuleComponent component)
    {
        return Loc.GetString(
            component.GuideAnnouncement,
            ("initialTc", component.InitialTelecrystals),
            ("stealPercent", (int) Math.Round(component.KillStealFraction * 100f)),
            ("maxSteal", component.MaxKillSteal),
            ("leaderReward", component.LeaderKillReward),
            ("comebackTc", component.ArenaComebackTelecrystals)
        );
    }

    /// <summary>
    /// Спавнит машины искупления в случайных местах на станции
    /// </summary>
    private void SpawnRedemptionMachines()
    {
        if (!TryGetActiveRuleEntity(out var activeRule) || !TryComp<SyndieBattleRuleComponent>(activeRule, out var ruleComp))
            return;

        var spawned = 0;
        var maxAttempts = ruleComp.RedemptionMachineCount * 50;
        var attempts = 0;

        while (spawned < ruleComp.RedemptionMachineCount && attempts < maxAttempts)
        {
            attempts++;

            // Берём случайную станцию
            if (!TryGetRandomStation(out var station) || !TryComp<StationDataComponent>(station, out var stationData))
                continue;

            if (!TryFindRandomTileOnStation((station.Value, stationData), out _, out var grid, out var coords))
                continue;

            if (TryComp<MapGridComponent>(grid, out var gridComp))
            {
                var indices = _mapSystem.TileIndicesFor(grid, gridComp, coords);
                var anchored = new List<EntityUid>();
                _mapSystem.GetAnchoredEntities((grid, gridComp), indices, anchored);

                if (anchored.Count != 0)
                {
                    Log.Warning($"Could not spawn a machine, there are {anchored.Count} anchored entities on the tile at {coords}.");
                    continue;
                }
            }

            var machine = Spawn(ruleComp.RedemptionMachine, coords);
            ruleComp.RedemptionMachines.Add(machine);
            Log.Info($"Spawned a machine at {coords}.");
            spawned++;
        }

        Log.Info($"Spawned {spawned} machines with {attempts} attempts.");
    }

    /// <summary>
    /// Получает имя убийцы из KillSource
    /// </summary>
    private string GetKillerName(KillSource source)
    {
        switch (source)
        {
            case KillPlayerSource player:
                if (!_playerManager.TryGetSessionById(player.PlayerId, out var session))
                    return Loc.GetString("syndiebattle-killer-unknown-player");
                return session.AttachedEntity == null ? Loc.GetString("syndiebattle-killer-unknown-player") : MetaData(session.AttachedEntity.Value).EntityName;

            case KillEnvironmentSource:
                return Loc.GetString("syndiebattle-killer-environment");
        }

        return Loc.GetString("syndiebattle-killer-unknown");
    }

    /// <summary>
    /// Получает место смерти
    /// </summary>
    private string GetDeathLocation(EntityUid entity)
    {
        var location = _navMapSystem.GetNearestBeaconString(entity);
        return string.IsNullOrEmpty(location) ? Loc.GetString("syndiebattle-location-unknown") : location;
    }
}
