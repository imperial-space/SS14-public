using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Imperial.Medieval.Achievements.Jobs;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.Achievements;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Containers;

namespace Content.Server.Imperial.Medieval.Achievements;

public sealed partial class AchievementSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly ActorSystem _actor = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly JobAchievementManager _jobAchievement = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    private readonly Dictionary<Guid, HashSet<string>> _playerAchievements = new();
    private readonly Dictionary<Guid, Dictionary<string, Dictionary<string, int>>> _roundProgression = new();

    public const string AchievementFirstJoin = "AchievementJoinSpellward";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);

        _playerManager.PlayerStatusChanged += OnPlayerChange;

        InitializeConditions();
        InitializeUI();
    }

    private async void OnPlayerChange(object? sender, SessionStatusEventArgs args)
    {
        var guid = args.Session.UserId.UserId;

        switch (args.NewStatus)
        {
            case SessionStatus.InGame:
                var achievements = await _dbManager.GetPlayerAchievements(guid);

                _playerAchievements[guid] = achievements
                    .Select(a => a.AchievementId)
                    .ToHashSet();

                var savedProgress = await _dbManager.GetPlayerAchievementProgress(guid);
                if (savedProgress.Count > 0)
                {
                    if (!_roundProgression.TryGetValue(guid, out var playerProg))
                    {
                        playerProg = new Dictionary<string, Dictionary<string, int>>();
                        _roundProgression[guid] = playerProg;
                    }

                    foreach (var (achId, keys) in savedProgress)
                    {
                        if (_playerAchievements[guid].Contains(achId))
                            continue;

                        playerProg[achId] = new Dictionary<string, int>(keys);
                    }
                }

                TryGrantAchievement(guid, AchievementFirstJoin, args.Session);
                break;

            case SessionStatus.Disconnected:
                _playerAchievements.Remove(guid);
                break;
        }
    }

    private async void OnRoundStarted(RoundStartedEvent args)
    {
        foreach (var session in _playerManager.Sessions)
        {
            var guid = session.UserId.UserId;

            if (!_playerAchievements.ContainsKey(guid))
                continue;

            var savedProgress = await _dbManager.GetPlayerAchievementProgress(guid);
            if (savedProgress.Count == 0)
                continue;

            if (!_roundProgression.TryGetValue(guid, out var playerProg))
            {
                playerProg = new Dictionary<string, Dictionary<string, int>>();
                _roundProgression[guid] = playerProg;
            }

            foreach (var (achId, keys) in savedProgress)
            {
                if (_playerAchievements[guid].Contains(achId))
                    continue;

                playerProg[achId] = new Dictionary<string, int>(keys);
            }
        }
    }

    private async void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        await SaveAllProgressAsync();
        _roundProgression.Clear();
    }

    private async Task SaveAllProgressAsync()
    {
        foreach (var (guid, playerProg) in _roundProgression)
        {
            var toSave = new Dictionary<string, Dictionary<string, int>>();
            foreach (var (achId, achProg) in playerProg)
            {
                if (!_protoManager.TryIndex<AchievementPrototype>(achId, out var proto))
                    continue;

                if (proto.RoundOnly)
                    continue;

                toSave[achId] = achProg;
            }

            if (toSave.Count > 0)
                await _dbManager.SavePlayerAchievementProgress(guid, toSave);
        }
    }

    public void TryGrantAchievement(EntityUid player, string achievementId, object? context = null)
    {
        var guid = GetPlayerGuid(player);
        if (guid == null)
            return;

        if (!_playerAchievements.TryGetValue(guid.Value, out var unlocked))
            return;

        if (unlocked.Contains(achievementId))
            return;

        if (!_protoManager.TryIndex<AchievementPrototype>(achievementId, out var prototype))
            return;

        if (!ArePrerequisitesMet(prototype, unlocked))
            return;

        if (!_roundProgression.TryGetValue(guid.Value, out var playerProg))
        {
            playerProg = new Dictionary<string, Dictionary<string, int>>();
            _roundProgression[guid.Value] = playerProg;
        }

        if (!playerProg.TryGetValue(achievementId, out var achievementProg))
        {
            achievementProg = new Dictionary<string, int>();
            playerProg[achievementId] = achievementProg;
        }

        var allMet = true;
        foreach (var condition in prototype.Conditions)
        {
            if (!condition.Check(player, EntityManager, _protoManager, context, achievementProg))
                allMet = false;
        }

        if (allMet)
        {
            unlocked.Add(achievementId);
            _ = _dbManager.GrantAchievement(guid.Value, achievementId);

            playerProg.Remove(achievementId);
            if (!prototype.RoundOnly)
                _ = _dbManager.DeletePlayerAchievementProgress(guid.Value, achievementId);

            if (_actor.TryGetSession(player, out var session))
            {
                RaiseNetworkEvent(new AchievementUnlockedEvent(achievementId), session!);
                _ = _jobAchievement.RecheckJobs(session!.UserId);
            }
        }
    }

    public void TryUpdateProgressAndGrant(EntityUid player, object context,
        Func<AchievementPrototype, bool> filter)
    {
        if (GetPlayerGuid(player) is not { } guid)
            return;

        if (!_playerAchievements.TryGetValue(guid, out var unlocked))
            return;

        if (!_roundProgression.TryGetValue(guid, out var playerProg))
        {
            playerProg = new Dictionary<string, Dictionary<string, int>>();
            _roundProgression[guid] = playerProg;
        }

        foreach (var ach in _protoManager.EnumeratePrototypes<AchievementPrototype>())
        {
            if (unlocked.Contains(ach.ID) || !filter(ach))
                continue;

            if (!ArePrerequisitesMet(ach, unlocked))
                continue;

            if (!playerProg.TryGetValue(ach.ID, out var achievementProg))
            {
                achievementProg = new Dictionary<string, int>();
                playerProg[ach.ID] = achievementProg;
            }

            var anyUpdated = false;
            foreach (var condition in ach.Conditions)
            {
                if (condition.TryUpdateProgress(player, EntityManager, _protoManager, context, achievementProg))
                    anyUpdated = true;
            }

            if (anyUpdated)
                TryGrantAchievement(player, ach.ID);
        }
    }

    public async Task<bool> TryGrantAchievement(Guid guid, string achievementId, ICommonSession? session = null)
    {
        if (!_playerAchievements.TryGetValue(guid, out var unlocked))
            return false;

        if (unlocked.Contains(achievementId))
            return false;

        if (!_protoManager.HasIndex<AchievementPrototype>(achievementId))
            return false;

        var success = await _dbManager.GrantAchievement(guid, achievementId);
        if (!success)
            return false;

        unlocked.Add(achievementId);

        if (_roundProgression.TryGetValue(guid, out var playerProg))
            playerProg.Remove(achievementId);

        if (_protoManager.TryIndex<AchievementPrototype>(achievementId, out var proto) && !proto.RoundOnly)
            await _dbManager.DeletePlayerAchievementProgress(guid, achievementId);

        if (session != null)
        {
            RaiseNetworkEvent(new AchievementUnlockedEvent(achievementId), session);
            _ = _jobAchievement.RecheckJobs(session.UserId);
        }

        return true;
    }

    public async Task<bool> TryRevokeAchievement(Guid guid, string achievementId, ICommonSession? session = null)
    {
        if (!_playerAchievements.TryGetValue(guid, out var unlocked))
            return false;

        if (!unlocked.Contains(achievementId))
            return false;

        var success = await _dbManager.RevokeAchievement(guid, achievementId);
        if (success)
        {
            unlocked.Remove(achievementId);

            if (_roundProgression.TryGetValue(guid, out var playerProg))
                playerProg.Remove(achievementId);

            if (session != null)
                _ = _jobAchievement.RecheckJobs(session.UserId);
        }

        return success;
    }

    public List<string> GetUnlockedAchievements(Guid guid)
    {
        if (_playerAchievements.TryGetValue(guid, out var achievements))
            return achievements.ToList();

        return new List<string>();
    }

    private Guid? GetPlayerGuid(EntityUid player)
    {
        if (!_actor.TryGetSession(player, out var session))
            return null;

        return session?.UserId.UserId;
    }
}
