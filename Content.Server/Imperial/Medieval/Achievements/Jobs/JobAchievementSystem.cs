using System.Collections.Immutable;
using Content.Server.GameTicking.Events;
using Content.Server.Station.Events;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Achievements.Jobs;

public sealed class JobAchievementSystem : EntitySystem
{
    [Dependency] private readonly JobAchievementManager _manager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private ImmutableArray<ProtoId<JobPrototype>> _gatedJobs = [];

    public override void Initialize()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<StationJobsGetCandidatesEvent>(OnStationJobsGetCandidates);
        SubscribeLocalEvent<IsRoleAllowedEvent>(OnIsRoleAllowed);
        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnGetDisallowedJobs);

        CacheGatedJobs();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs ev)
    {
        if (ev.WasModified<JobPrototype>())
            CacheGatedJobs();
    }

    private void CacheGatedJobs()
    {
        var builder = ImmutableArray.CreateBuilder<ProtoId<JobPrototype>>();
        foreach (var job in _prototypes.EnumeratePrototypes<JobPrototype>())
        {
            if (job.RequiredAchievements.Count > 0)
                builder.Add(job.ID);
        }
        _gatedJobs = builder.ToImmutable();
    }

    private void OnStationJobsGetCandidates(ref StationJobsGetCandidatesEvent ev)
    {
        for (var i = ev.Jobs.Count - 1; i >= 0; i--)
        {
            var jobId = ev.Jobs[i];
            if (_player.TryGetSessionById(ev.Player, out var player) &&
                !_manager.IsAllowed(player, jobId))
            {
                ev.Jobs.RemoveSwap(i);
            }
        }
    }

    private void OnIsRoleAllowed(ref IsRoleAllowedEvent ev)
    {
        if (ev.Jobs is null)
            return;

        foreach (var proto in ev.Jobs)
        {
            if (!_manager.IsAllowed(ev.Player, proto))
                ev.Cancelled = true;
        }
    }

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent ev)
    {
        foreach (var job in _gatedJobs)
        {
            if (!_manager.IsAllowed(ev.Player, job))
                ev.Jobs.Add(job);
        }
    }
}
