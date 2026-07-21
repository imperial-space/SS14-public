using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.Imperial.Medieval.Achievements;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Serilog;

namespace Content.Server.Imperial.Medieval.Achievements.Jobs;

public sealed class JobAchievementManager : IPostInjectInit
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly UserDbDataManager _userDb = default!;

    private readonly Dictionary<NetUserId, HashSet<string>> _unlockedJobs = new();

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgJobAchievements>();
    }

    private async Task LoadData(ICommonSession session, CancellationToken cancel)
    {
        var earnedAchievements = (await _db.GetPlayerAchievements(session.UserId.UserId, cancel))
            .Select(a => a.AchievementId)
            .ToHashSet();

        cancel.ThrowIfCancellationRequested();

        var unlocked = new HashSet<string>();

        foreach (var job in _prototypes.EnumeratePrototypes<JobPrototype>())
        {
            if (job.RequiredAchievements.Count == 0)
                continue;

            if (job.RequiredAchievements.All(a => earnedAchievements.Contains(a)))
                unlocked.Add(job.ID);
        }

        _unlockedJobs[session.UserId] = unlocked;
    }

    private void FinishLoad(ICommonSession session)
    {
        SendToClient(session);
    }

    private void ClientDisconnected(ICommonSession session)
    {
        _unlockedJobs.Remove(session.UserId);
    }

    public bool IsAllowed(ICommonSession session, ProtoId<JobPrototype> job)
    {
        if (!_prototypes.Resolve(job, out var jobProto) || jobProto.RequiredAchievements.Count == 0)
            return true;

        return IsUnlocked(session.UserId, job);
    }

    public bool IsUnlocked(NetUserId player, ProtoId<JobPrototype> job)
    {
        if (!_unlockedJobs.TryGetValue(player, out var unlocked))
            return false;

        return unlocked.Contains(job);
    }

    public async Task RecheckJobs(NetUserId player)
    {
        var earnedAchievements = (await _db.GetPlayerAchievements(player.UserId))
            .Select(a => a.AchievementId)
            .ToHashSet();

        if (!_unlockedJobs.TryGetValue(player, out var unlocked))
            return;

        var changed = false;

        foreach (var job in _prototypes.EnumeratePrototypes<JobPrototype>())
        {
            if (job.RequiredAchievements.Count == 0)
                continue;

            var isMet = job.RequiredAchievements.All(a => earnedAchievements.Contains(a));
            changed |= isMet ? unlocked.Add(job.ID) : unlocked.Remove(job.ID);
        }

        if (changed && _player.TryGetSessionById(player, out var session))
            SendToClient(session);
    }

    public void SendToClient(ICommonSession session)
    {
        var msg = new MsgJobAchievements
        {
            Achievements = _unlockedJobs.GetValueOrDefault(session.UserId) ?? new HashSet<string>()
        };

        _net.ServerSendMessage(msg, session.Channel);
    }

    void IPostInjectInit.PostInject()
    {
        _userDb.AddOnLoadPlayer(LoadData);
        _userDb.AddOnFinishLoad(FinishLoad);
        _userDb.AddOnPlayerDisconnect(ClientDisconnected);
    }
}
