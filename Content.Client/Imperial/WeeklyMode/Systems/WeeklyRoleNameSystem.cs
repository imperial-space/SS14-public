using Content.Client.GameTicking.Managers;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.WeeklyMode.Systems;

public sealed class WeeklyRoleNameSystem : EntitySystem
{
    [Dependency] private readonly ClientGameTicker _ticker = default!;
    private readonly Dictionary<ProtoId<JobPrototype>, string> _names = new();

    public event Action? NamesUpdated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TickerJobsAvailableEvent>(OnJobsAvailable);
        _ticker.LobbyJobsAvailableUpdated += OnLobbyJobsAvailableUpdated;
    }

    public override void Shutdown()
    {
        _ticker.LobbyJobsAvailableUpdated -= OnLobbyJobsAvailableUpdated;
        base.Shutdown();
    }

    private void OnJobsAvailable(TickerJobsAvailableEvent message)
    {
        _names.Clear();
        foreach (var (job, name) in message.JobNameOverrides)
            _names[job] = name;

        // Also refresh if this handler runs after ClientGameTicker's handler.
        NamesUpdated?.Invoke();
    }

    private void OnLobbyJobsAvailableUpdated(IReadOnlyDictionary<NetEntity, Dictionary<ProtoId<JobPrototype>, int?>> jobs)
    {
        NamesUpdated?.Invoke();
    }

    public string GetJobDisplayName(JobPrototype job)
    {
        return _names.TryGetValue(job.ID, out var alias) ? alias : job.LocalizedName;
    }

    public string GetJobDisplayName(ProtoId<JobPrototype> id, IPrototypeManager prototypes)
    {
        return _names.TryGetValue(id, out var alias) ? alias
            : prototypes.TryIndex(id, out var job) ? job.LocalizedName : id.Id;
    }
}
