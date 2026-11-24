using System.Linq;
using Content.Server.Imperial.SyndieBattle.Components;
using Content.Server.Imperial.SyndieBattle.Types;
using Content.Shared.Chat;
using Content.Shared.Imperial.SyndieBattle.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Store.Components;
using Robust.Shared.Network;

namespace Content.Server.Imperial.SyndieBattle;

public sealed partial class SyndieBattleRuleSystem
{
    private Entity<SyndieBattlePersistentStoreComponent> EnsurePersistentStore(EntityUid owner, SyndieBattleRuleComponent rule, SyndieBattlePlayerScore score)
    {
        if (TryGetPersistentStore(score, out var existing))
            return existing;

        var persistent = EnsureComp<SyndieBattlePersistentStoreComponent>(owner);
        persistent.Balance.Clear();
        persistent.Balance[rule.Currency] = rule.InitialTelecrystals;
        score.PersistentStoreEntity = owner;
        return (owner, persistent);
    }

    public int EnsureComebackBalance(SyndieBattleRuleComponent rule, SyndieBattlePlayerScore score)
    {
        if (!TryGetPersistentStore(score, out var persistent))
            return 0;

        var balance = GetBalance(persistent.Comp, rule);
        var granted = Math.Max(0, rule.ArenaComebackTelecrystals - balance);
        if (granted == 0)
            return 0;

        persistent.Comp.Balance[rule.Currency] = balance + granted;
        score.ComebackTelecrystals += granted;
        return granted;
    }

    public bool TryRedeemPda(
        SyndieBattleRuleComponent rule,
        SyndieBattlePlayerScore recipient,
        Entity<SyndieBattlePdaOwnerComponent> pda,
        SyndieBattleRedemptionComponent machine,
        out int payout)
    {
        payout = 0;
        if (!pda.Comp.RedemptionAllowed ||
            !rule.PlayerScores.TryGetValue(pda.Comp.UserId, out var victim) ||
            victim.StoreEntity != pda.Owner || victim.Generation != pda.Comp.Generation ||
            !TryGetPersistentStore(victim, out var source) ||
            !TryGetPersistentStore(recipient, out var destination) ||
            source.Owner == destination.Owner)
        {
            return false;
        }

        var balance = GetBalance(source.Comp, rule);
        if (balance <= 0)
            return false;

        var proportional = (int)Math.Floor(balance * machine.StoredTelecrystalMultiplier);
        var reward = proportional > 0 ? proportional : machine.BaseReward;
        reward = Math.Min(balance, Math.Min(machine.MaxReward, reward));
        if (reward <= 0)
            return false;

        pda.Comp.RedemptionAllowed = false;
        source.Comp.Balance[rule.Currency] = balance - reward;
        destination.Comp.Balance[rule.Currency] = GetBalance(destination.Comp, rule) + reward;
        payout = reward;
        SyncLiveStore(victim, source.Comp);
        SyncLiveStore(recipient, destination.Comp);
        return true;
    }

    private SyndieBattleCurrencyTransfer TransferTelecrystals(SyndieBattlePlayerScore victim, SyndieBattlePlayerScore killer, SyndieBattleRuleComponent rule, int bonus)
    {
        if (!TryGetPersistentStore(victim, out var source) ||
            !TryGetPersistentStore(killer, out var destination) ||
            source.Owner == destination.Owner)
        {
            return default;
        }

        var sourceBalance = GetBalance(source.Comp, rule);
        var stolen = Math.Min(rule.MaxKillSteal, (int) Math.Floor(sourceBalance * rule.KillStealFraction));
        var reward = stolen + bonus;
        source.Comp.Balance[rule.Currency] = sourceBalance - stolen;
        destination.Comp.Balance[rule.Currency] = GetBalance(destination.Comp, rule) + reward;
        SyncLiveStore(victim, source.Comp);
        SyncLiveStore(killer, destination.Comp);
        return new SyndieBattleCurrencyTransfer(stolen, reward);
    }

    private bool TryGetPersistentStore(SyndieBattlePlayerScore score, out Entity<SyndieBattlePersistentStoreComponent> store)
    {
        store = default;
        if (score.PersistentStoreEntity is not { } owner ||
            Deleted(owner) ||
            !TryComp<SyndieBattlePersistentStoreComponent>(owner, out var component))
        {
            return false;
        }

        store = (owner, component);
        return true;
    }

    private static int GetBalance(SyndieBattlePersistentStoreComponent store, SyndieBattleRuleComponent rule)
    {
        return store.Balance.GetValueOrDefault(rule.Currency);
    }

    private static void CopyPersistentBalanceToStore(Entity<SyndieBattlePersistentStoreComponent> source, Entity<StoreComponent> destination)
    {
        destination.Comp.Balance.Clear();
        foreach (var (currency, amount) in source.Comp.Balance)
        {
            destination.Comp.Balance[currency] = amount;
        }
    }

    private static void CopyStoreBalanceToPersistent(Entity<StoreComponent> source, Entity<SyndieBattlePersistentStoreComponent> destination)
    {
        destination.Comp.Balance.Clear();
        foreach (var (currency, amount) in source.Comp.Balance)
        {
            destination.Comp.Balance[currency] = (int) amount;
        }
    }

    private void SyncLiveStore(SyndieBattlePlayerScore score, SyndieBattlePersistentStoreComponent source)
    {
        if (!TryGetPlayerStore(score, out var destination))
            return;

        destination.Comp.Balance.Clear();
        foreach (var (currency, amount) in source.Balance)
        {
            destination.Comp.Balance[currency] = amount;
        }

        _storeSystem.UpdateUserInterface(null, destination.Owner, destination.Comp);
    }

    private bool IsCreditedKill(SyndieBattleRuleComponent rule, NetUserId killer, NetUserId victim)
    {
        var key = (killer, victim);
        var now = Timing.CurTime;
        if (rule.RecentKills.TryGetValue(key, out var lastKill) && now - lastKill < rule.RepeatKillCooldown)
            return false;

        rule.RecentKills[key] = now;
        return true;
    }

    private void SetPdaRedemptionAllowed(SyndieBattlePlayerScore score, bool allowed)
    {
        if (score.StoreEntity is { } store &&
            TryComp<SyndieBattlePdaOwnerComponent>(store, out var owner))
        {
            owner.RedemptionAllowed = allowed;
        }
    }

    private void AnnounceKillStreak(SyndieBattleRuleComponent rule, SyndieBattlePlayerScore score)
    {
        if (!rule.KillStreakAnnouncements.Contains(score.CurrentKillStreak))
            return;

        var message = Loc.GetString("syndiebattle-kill-streak", ("name", score.Name), ("kills", score.CurrentKillStreak));
        _chatManager.ChatMessageToAll(ChatChannel.Server, message, message, default, false, false);
    }

    private void SendAntiFarmMessage(NetUserId userId, TimeSpan cooldown)
    {
        if (!_playerManager.TryGetSessionById(userId, out var session))
            return;

        var message = Loc.GetString("syndiebattle-repeat-kill-blocked", ("minutes", (int) Math.Ceiling(cooldown.TotalMinutes)));
        _chatManager.ChatMessageToOne(ChatChannel.Server, message, message, default, false, session.Channel);
    }

    private void AnnounceRoundTime(SyndieBattleRuleComponent rule)
    {
        if (rule.RoundEndsAt is not { } roundEndsAt ||
            !rule.PendingRoundTimeAnnouncements.TryPeek(out var announcement) ||
            roundEndsAt - Timing.CurTime > announcement)
        {
            return;
        }

        rule.PendingRoundTimeAnnouncements.Dequeue();
        var message = Loc.GetString("syndiebattle-time-remaining", ("time", announcement.ToString(@"mm\:ss")));
        _chatManager.ChatMessageToAll(ChatChannel.Server, message, message, default, false, false);
    }

    public void UpdateLeader(SyndieBattleRuleComponent rule)
    {
        NetUserId? nextUser = null;
        SyndieBattlePlayerScore? nextScore = null;
        foreach (var (user, score) in rule.PlayerScores)
        {
            if (score.KillCount < rule.LeaderKillThreshold || !CanLeadBattle(score))
                continue;

            var higherScore = nextScore == null || score.KillCount > nextScore.KillCount;
            var winsTie = nextScore != null && score.KillCount == nextScore.KillCount && (user == rule.Leader || nextUser != rule.Leader && score.ParticipationOrder < nextScore.ParticipationOrder);
            if (!higherScore && !winsTie)
                continue;

            nextUser = user;
            nextScore = score;
        }

        if (nextUser == rule.Leader)
            return;
        if (nextUser is { } next && nextScore != null)
            SetLeader(rule, next, nextScore);
        else
            ClearLeader(rule);
    }

    private bool CanLeadBattle(SyndieBattlePlayerScore score)
    {
        return score.Alive && score.CurrentEntity is { } entity && !TerminatingOrDeleted(entity) &&
               !HasComp<SyndieBattleArenaCombatantComponent>(entity) &&
               TryComp<MobStateComponent>(entity, out var state) && !_mobStateSystem.IsDead(entity, state);
    }

    private void SetLeader(SyndieBattleRuleComponent rule, NetUserId userId, SyndieBattlePlayerScore score)
    {
        if (rule.Leader is { } oldLeader &&
            rule.PlayerScores.TryGetValue(oldLeader, out var oldScore) &&
            oldScore.CurrentEntity is { } oldEntity)
        {
            RemCompDeferred<SyndieBattleLeaderComponent>(oldEntity);
        }

        rule.Leader = userId;
        if (score.Alive && score.CurrentEntity is { } entity && !Deleted(entity))
            SetLeaderIcon(entity, rule);

        var message = Loc.GetString("syndiebattle-leader-changed", ("name", score.Name), ("kills", score.KillCount));
        _chatManager.ChatMessageToAll(ChatChannel.Server, message, message, default, false, false);

        if (_playerManager.TryGetSessionById(userId, out var session))
        {
            var personalMessage = Loc.GetString("syndiebattle-leader-personal");
            _chatManager.ChatMessageToOne(ChatChannel.Server, personalMessage, personalMessage, default, false, session.Channel);
        }
    }

    private void SetLeaderIcon(EntityUid entity, SyndieBattleRuleComponent rule)
    {
        var leader = EnsureComp<SyndieBattleLeaderComponent>(entity);
        leader.StatusIcon = rule.LeaderStatusIcon;
        Dirty(entity, leader);
    }

    private void ClearLeader(SyndieBattleRuleComponent rule)
    {
        if (rule.Leader is { } leader &&
            rule.PlayerScores.TryGetValue(leader, out var score) &&
            score.CurrentEntity is { } entity)
        {
            RemCompDeferred<SyndieBattleLeaderComponent>(entity);
        }

        rule.Leader = null;
    }

    private void FinishByKillLimit(SyndieBattleRuleComponent rule, NetUserId userId, SyndieBattlePlayerScore score)
    {
        if (score.KillCount < rule.VictoryKillLimit)
            return;

        FinishRound(rule, userId, score, SyndieBattleRoundEndReason.KillLimit);
    }

    private void FinishByTimeLimit(SyndieBattleRuleComponent rule)
    {
        var winner = rule.PlayerScores
            .OrderByDescending(entry => entry.Value.KillCount)
            .ThenByDescending(entry => entry.Value.StolenTelecrystals)
            .ThenByDescending(entry => TryGetPersistentStore(entry.Value, out var store) ? GetBalance(store.Comp, rule) : 0)
            .ThenBy(entry => entry.Value.ParticipationOrder)
            .FirstOrDefault();

        if (winner.Value == null)
        {
            rule.RoundEndsAt = null;
            _roundEndSystem.EndRound(rule.RoundEndDelay);
            return;
        }

        FinishRound(rule, winner.Key, winner.Value, SyndieBattleRoundEndReason.TimeLimit);
    }

    private void FinishRound(SyndieBattleRuleComponent rule, NetUserId userId, SyndieBattlePlayerScore score, SyndieBattleRoundEndReason reason)
    {
        if (rule.Winner != null)
            return;

        rule.Winner = userId;
        rule.WinnerName = score.Name;
        rule.WinnerKills = score.KillCount;
        rule.RoundEndReason = reason;
        rule.RoundEndsAt = null;

        var message = Loc.GetString(
            reason == SyndieBattleRoundEndReason.KillLimit ? "syndiebattle-victory-kill-limit" : "syndiebattle-victory-time-limit",
            ("name", score.Name),
            ("kills", score.KillCount)
        );
        _chatManager.ChatMessageToAll(ChatChannel.Server, message, message, default, false, false);
        _roundEndSystem.EndRound(rule.RoundEndDelay);
    }
}
