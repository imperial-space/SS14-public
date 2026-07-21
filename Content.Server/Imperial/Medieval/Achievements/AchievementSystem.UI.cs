using System.Linq;
using System.Threading.Tasks;
using Content.Shared.Imperial.Medieval.Achievements;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Achievements;

public sealed partial class AchievementSystem
{
    private Dictionary<string, float> _globalPercentsCache = new();
    private TimeSpan _lastStatsUpdate;
    private bool _statsUpdateInProgress = false;

    private const double StatsCacheSeconds = 60.0;

    private void InitializeUI()
    {
        SubscribeNetworkEvent<RequestAchievementMenuDataEvent>(OnMenuDataRequested);
    }

    private async void OnMenuDataRequested(RequestAchievementMenuDataEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var guid = session.UserId.UserId;

        if (!_playerAchievements.TryGetValue(guid, out var unlocked))
            unlocked = new HashSet<string>();

        var progressPerAchievement = BuildProgressSnapshot(guid);

        var now = Timing.RealTime;
        if ((now - _lastStatsUpdate).TotalSeconds > StatsCacheSeconds && !_statsUpdateInProgress)
        {
            _statsUpdateInProgress = true;
            _globalPercentsCache = await BuildGlobalPercents();
            _lastStatsUpdate = now;
            _statsUpdateInProgress = false;
        }

        RaiseNetworkEvent(
            new AchievementMenuDataEvent(
                new HashSet<string>(unlocked),
                new Dictionary<string, float>(_globalPercentsCache),
                progressPerAchievement),
            session);
    }

    private Dictionary<string, Dictionary<string, int>> BuildProgressSnapshot(Guid guid)
    {
        var result = new Dictionary<string, Dictionary<string, int>>();

        if (!_roundProgression.TryGetValue(guid, out var playerData))
            return result;

        foreach (var (achId, condProgress) in playerData)
        {
            result[achId] = new Dictionary<string, int>(condProgress);
        }

        return result;
    }

    private async Task<Dictionary<string, float>> BuildGlobalPercents()
    {
        var result = new Dictionary<string, float>();

        var (totalPlayers, stats) = await _dbManager.GetAchievementStats();

        if (totalPlayers == 0)
            return result;

        foreach (var proto in _protoManager.EnumeratePrototypes<AchievementPrototype>())
        {
            result[proto.ID] = stats.TryGetValue(proto.ID, out var count)
                ? (float)count / totalPlayers * 100f
                : 0f;
        }

        return result;
    }
}
