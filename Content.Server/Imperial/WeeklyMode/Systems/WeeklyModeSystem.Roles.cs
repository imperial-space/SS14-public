using Content.Server.WeeklyMode.Components;
using Content.Shared.Imperial.ICCVar;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Cargo.Systems;
using Content.Server.Chemistry.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Light.EntitySystems;
using Content.Server.Maps;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Research.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Actions;
using Content.Shared.CartridgeLoader;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Chat;
using Content.Shared.Containers;
using Content.Shared.Containers.ItemSlots;
using Content.Server.WeeklyMode.Storage;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Light.Components;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Maps;
using Content.Shared.Materials;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.VendingMachines;
using Content.Shared.WeeklyMode;
using Robust.Server.Containers;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server.WeeklyMode.Systems;

public sealed partial class WeeklyModeSystem
{
    public bool TryDisableRoles(string setId, IReadOnlyList<string> jobIds, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (jobIds.Count is < 1 or > 10)
        {
            message = "Disable role batch must contain between 1 and 10 job IDs.";
            return false;
        }

        foreach (var jobId in jobIds)
        {
            if (!_prototype.TryIndex<JobPrototype>(jobId, out _))
            {
                message = $"Unknown job prototype '{jobId}'. No roles were changed.";
                return false;
            }
        }

        var originalSet = CloneSet(set);
        var added = new List<string>();
        foreach (var jobId in jobIds.Distinct(StringComparer.Ordinal))
        {
            if (set.DefaultDisabledJobs.Contains(jobId))
                continue;

            set.DefaultDisabledJobs.Add(jobId);
            added.Add(jobId);
        }

        set.DefaultDisabledJobs.Sort(StringComparer.Ordinal);
        if (!TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Roles, out message))
            return false;

        message = added.Count == 0
            ? $"No role changes were needed for set '{set.SetId}'."
            : $"Disabled roles for set '{set.SetId}': {string.Join(", ", added)}.";
        return true;
    }

    public bool TryEnableRoles(string setId, IReadOnlyList<string> jobIds, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var removed = new List<string>();
        foreach (var jobId in jobIds)
        {
            if (set.DefaultDisabledJobs.Remove(jobId))
                removed.Add(jobId);
        }

        _store.SaveSet(set);

        message = removed.Count == 0
            ? $"No role changes were needed for set '{set.SetId}'."
            : $"Enabled roles for set '{set.SetId}': {string.Join(", ", removed)}.";
        return true;
    }

    public bool TryRenameRole(string setId, string jobId, string alias, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (!_prototype.TryIndex<JobPrototype>(jobId, out _))
        {
            message = $"Unknown job prototype '{jobId}'.";
            return false;
        }

        if (!TryNormalizeAlias(alias, out alias, out message))
            return false;

        set.DefaultRoleAliases.TryGetValue(jobId, out var oldAlias);
        set.DefaultRoleAliases[jobId] = alias;
        _store.SaveSet(set);

        message = $"Renamed role '{jobId}' to '{alias}' for weekly set '{set.SetId}'.";
        _sawmill.Info($"Role alias changed for set '{set.SetId}' job '{jobId}': old='{oldAlias ?? "<none>"}', new='{alias}'.");
        return true;
    }

    public bool TryRenameRolesBatch(string setId, string batch, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var parsed = new List<(string JobId, string Alias)>();
        foreach (var entry in batch.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = entry.IndexOf('=');
            if (separator <= 0 || separator == entry.Length - 1)
            {
                message = $"Invalid alias entry '{entry}'. Use jobId=alias;jobId=alias.";
                return false;
            }

            var jobId = entry[..separator].Trim();
            var alias = entry[(separator + 1)..].Trim().Trim('"');

            if (!_prototype.TryIndex<JobPrototype>(jobId, out _))
            {
                message = $"Unknown job prototype '{jobId}'.";
                return false;
            }

            if (!TryNormalizeAlias(alias, out alias, out message))
                return false;

            parsed.Add((jobId, alias));
        }

        var changed = new List<string>();
        foreach (var (jobId, alias) in parsed)
        {
            set.DefaultRoleAliases.TryGetValue(jobId, out var oldAlias);
            set.DefaultRoleAliases[jobId] = alias;
            changed.Add(jobId);
            _sawmill.Info($"Role alias changed for set '{set.SetId}' job '{jobId}': old='{oldAlias ?? "<none>"}', new='{alias}'.");
        }

        _store.SaveSet(set);
        message = changed.Count == 0
            ? $"No aliases were changed for set '{set.SetId}'."
            : $"Updated aliases for set '{set.SetId}': {string.Join(", ", changed)}.";
        return true;
    }

    public string ListRoleAliases(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        if (set.DefaultRoleAliases.Count == 0)
            return $"Weekly set '{set.SetId}' has no role aliases.";

        var lines = new List<string> { $"Role aliases for weekly set '{set.SetId}':" };
        foreach (var (jobId, alias) in set.DefaultRoleAliases.OrderBy(x => x.Key, StringComparer.Ordinal))
            lines.Add($"- {jobId}: {alias}");

        return string.Join('\n', lines);
    }

    public bool TryClearRoleAliases(string setId, IReadOnlyList<string> jobIds, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (jobIds.Count > 0)
        {
            foreach (var jobId in jobIds)
            {
                if (!_prototype.TryIndex<JobPrototype>(jobId, out _))
                {
                    message = $"Unknown job prototype '{jobId}'. No aliases were changed.";
                    return false;
                }
            }
        }

        var removed = new List<string>();
        if (jobIds.Count == 0)
        {
            removed.AddRange(set.DefaultRoleAliases.Keys);
            set.DefaultRoleAliases.Clear();
        }
        else
        {
            foreach (var jobId in jobIds.Distinct(StringComparer.Ordinal))
            {
                if (set.DefaultRoleAliases.Remove(jobId))
                    removed.Add(jobId);
            }
        }

        _store.SaveSet(set);
        message = removed.Count == 0
            ? $"No aliases were cleared for set '{set.SetId}'."
            : $"Cleared aliases for set '{set.SetId}': {string.Join(", ", removed)}.";
        _sawmill.Info(message);
        return true;
    }

    public bool TryClearRoles(string setId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        set.DefaultDisabledJobs.Clear();
        set.DefaultRoleAliases.Clear();
        set.DefaultRoleLimits.Clear();
        _store.SaveSet(set);

        message = $"Cleared disabled roles, aliases, and limits for set '{set.SetId}'.";
        return true;
    }

    public bool TrySetRoleLimit(string setId, string jobId, int count, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (!_prototype.TryIndex<JobPrototype>(jobId, out _))
        {
            message = $"Unknown job prototype '{jobId}'.";
            return false;
        }

        if (count < 0)
        {
            message = "Role limit may not be negative.";
            return false;
        }

        var originalSet = CloneSet(set);
        set.DefaultRoleLimits[jobId] = count;
        if (!TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Roles, out message))
            return false;

        message = $"Set role limit for '{jobId}' to {count} in weekly set '{set.SetId}'.";
        return true;
    }

    public string ListRoleLimits(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        if (set.DefaultRoleLimits.Count == 0)
            return $"Weekly set '{set.SetId}' has no role limits.";

        var lines = new List<string> { $"Role limits for weekly set '{set.SetId}':" };
        foreach (var (jobId, count) in set.DefaultRoleLimits.OrderBy(x => x.Key, StringComparer.Ordinal))
            lines.Add($"- {jobId}: {count}");

        return string.Join('\n', lines);
    }

    public bool TryClearRoleLimit(string setId, string jobId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (!_prototype.TryIndex<JobPrototype>(jobId, out _))
        {
            message = $"Unknown job prototype '{jobId}'.";
            return false;
        }

        if (!set.DefaultRoleLimits.Remove(jobId))
        {
            message = $"Weekly set '{set.SetId}' had no role limit for '{jobId}'.";
            return true;
        }

        _store.SaveSet(set);
        message = $"Cleared role limit for '{jobId}' in weekly set '{set.SetId}'.";
        return true;
    }

    public bool TryClearRoleLimits(string setId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var removed = set.DefaultRoleLimits.Count;
        set.DefaultRoleLimits.Clear();
        _store.SaveSet(set);
        message = removed == 0
            ? $"Weekly set '{set.SetId}' had no role limits."
            : $"Cleared {removed} role limits for weekly set '{set.SetId}'.";
        return true;
    }

    public bool TryForceRole(
        string setId,
        NetUserId userId,
        string lastKnownCKey,
        string jobId,
        bool bypassPlaytime,
        string createdBy,
        out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (!TryValidateForcedRoleJob(set, jobId, out message))
            return false;

        var originalSet = CloneSet(set);
        var existing = set.ForcedRoleAssignments.FirstOrDefault(assignment => IsForcedAssignmentFor(assignment, userId));
        if (existing != null)
        {
            existing.JobId = jobId;
            existing.LastKnownCKey = lastKnownCKey;
            existing.BypassPlaytime = bypassPlaytime;
            existing.CreatedBy = createdBy;
            existing.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            set.ForcedRoleAssignments.Add(new WeeklyForcedRoleAssignment
            {
                PlayerNetUserId = userId.UserId.ToString(),
                LastKnownCKey = lastKnownCKey,
                JobId = jobId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                BypassPlaytime = bypassPlaytime,
            });
        }

        SortForcedRoleAssignments(set);

        if (!TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Roles, out message))
            return false;

        message = existing == null
            ? $"Forced role assignment saved: {lastKnownCKey} [{userId}] -> {jobId} (bypassPlaytime={bypassPlaytime})."
            : $"Forced role assignment updated: {lastKnownCKey} [{userId}] -> {jobId} (bypassPlaytime={bypassPlaytime}).";

        if (IsActiveSet(set.SetId) && _gameTicker.UserHasJoinedGame(userId))
            message += $"\nPlayer is already spawned. The new {jobId} assignment will apply on the next valid job assignment.";

        _sawmill.Info($"Admin {createdBy} forced account {lastKnownCKey} ({userId}) to job {jobId} in campaign {set.SetId}. bypassPlaytime={bypassPlaytime}");
        return true;
    }

    public bool TryUpdateForcedRole(
        string setId,
        NetUserId userId,
        string lastKnownCKey,
        string jobId,
        string updatedBy,
        bool? bypassPlaytime,
        out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (!TryValidateForcedRoleJob(set, jobId, out message))
            return false;

        var assignment = set.ForcedRoleAssignments.FirstOrDefault(assignment => IsForcedAssignmentFor(assignment, userId));
        if (assignment == null)
        {
            message = $"Weekly set '{set.SetId}' has no forced role assignment for '{lastKnownCKey}'.";
            return false;
        }

        var originalSet = CloneSet(set);
        assignment.JobId = jobId;
        assignment.LastKnownCKey = lastKnownCKey;
        assignment.CreatedBy = updatedBy;
        assignment.CreatedAt = DateTime.UtcNow;
        if (bypassPlaytime != null)
            assignment.BypassPlaytime = bypassPlaytime.Value;

        SortForcedRoleAssignments(set);

        if (!TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Roles, out message))
            return false;

        message = $"Forced role assignment updated: {lastKnownCKey} [{userId}] -> {jobId} (bypassPlaytime={assignment.BypassPlaytime}).";
        if (IsActiveSet(set.SetId) && _gameTicker.UserHasJoinedGame(userId))
            message += $"\nPlayer is already spawned. The new {jobId} assignment will apply on the next valid job assignment.";

        _sawmill.Info($"Admin {updatedBy} updated forced role assignment for account {lastKnownCKey} ({userId}) to job {jobId} in campaign {set.SetId}. bypassPlaytime={assignment.BypassPlaytime}");
        return true;
    }

    public string ListForcedRoles(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        if (set.ForcedRoleAssignments.Count == 0)
            return $"Weekly set '{set.SetId}' has no forced role assignments.";

        var lines = new List<string> { $"Forced role assignments for {set.SetId}:" };
        foreach (var assignment in set.ForcedRoleAssignments.OrderBy(assignment => assignment.LastKnownCKey, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"- {FormatForcedRoleAssignment(assignment)}");
        }

        return string.Join('\n', lines);
    }

    public string ShowForcedRole(string setId, NetUserId userId, string playerNameOrId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var assignment = set.ForcedRoleAssignments.FirstOrDefault(assignment => IsForcedAssignmentFor(assignment, userId));
        return assignment == null
            ? $"Weekly set '{set.SetId}' has no forced role assignment for '{playerNameOrId}'."
            : FormatForcedRoleAssignment(assignment);
    }

    public bool TryClearForcedRole(string setId, NetUserId userId, string playerNameOrId, string removedBy, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);
        var removed = set.ForcedRoleAssignments.RemoveAll(assignment => IsForcedAssignmentFor(assignment, userId));
        if (removed == 0)
        {
            message = $"Weekly set '{set.SetId}' had no forced role assignment for '{playerNameOrId}'.";
            return true;
        }

        if (!TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Roles, out message))
            return false;

        message = $"Cleared forced role assignment for {playerNameOrId} [{userId}] in weekly set '{set.SetId}'.";
        _sawmill.Info($"Admin {removedBy} cleared forced role assignment for account {playerNameOrId} ({userId}) in campaign {set.SetId}.");
        return true;
    }

    public bool TryClearAllForcedRoles(string setId, string removedBy, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var removed = set.ForcedRoleAssignments.Count;
        if (removed == 0)
        {
            message = $"Weekly set '{set.SetId}' had no forced role assignments.";
            return true;
        }

        var originalSet = CloneSet(set);
        set.ForcedRoleAssignments.Clear();
        if (!TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Roles, out message))
            return false;

        message = $"Cleared {removed} forced role assignments for weekly set '{set.SetId}'.";
        _sawmill.Info($"Admin {removedBy} cleared all forced role assignments in campaign {set.SetId}.");
        return true;
    }

    public async Task<(bool Success, NetUserId UserId, string LastKnownCKey, string Message)> ResolveForcedRoleTargetAsync(string playerNameOrId)
    {
        if (Guid.TryParse(playerNameOrId, out var guid))
        {
            var userId = new NetUserId(guid);
            var located = await _playerLocator.LookupIdAsync(userId);
            return (true, userId, located?.Username ?? userId.UserId.ToString(), string.Empty);
        }

        var data = await _playerLocator.LookupIdByNameAsync(playerNameOrId);
        if (data == null)
        {
            return (false,
                new NetUserId(Guid.Empty),
                string.Empty,
                $"Could not resolve '{playerNameOrId}' to a stable NetUserId. Use an online/current CKey, a known offline account name, or a UUID.");
        }

        return (true, data.UserId, data.Username, string.Empty);
    }

    public void ApplyForcedRoundStartAssignments(
        Dictionary<NetUserId, HumanoidCharacterProfile> profiles,
        IReadOnlyList<EntityUid> stations,
        Dictionary<EntityUid, Dictionary<ProtoId<JobPrototype>, int?>> stationJobs,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assigned)
    {
        var set = LoadActiveSetOrNull();
        if (set == null || set.ForcedRoleAssignments.Count == 0)
            return;

        foreach (var assignment in set.ForcedRoleAssignments.OrderBy(assignment => assignment.JobId, StringComparer.Ordinal))
        {
            if (!TryParseForcedRoleAssignment(assignment, out var userId, out var jobId, out var error))
            {
                _sawmill.Warning($"Weekly forced role assignment failed: campaign={set.SetId} reason={error}");
                continue;
            }

            if (!_prototype.TryIndex<JobPrototype>(jobId, out _))
            {
                _sawmill.Warning($"Weekly forced role assignment failed: campaign={set.SetId} player={assignment.LastKnownCKey} job={jobId} reason=unknown job");
                continue;
            }

            var hasReadyProfile = profiles.ContainsKey(userId);
            if (hasReadyProfile)
                profiles.Remove(userId);

            if (!TryReserveForcedRoundStartSlot(stations, stationJobs, jobId, out var station))
            {
                _sawmill.Warning($"Weekly forced role assignment failed: campaign={set.SetId} player={assignment.LastKnownCKey} userId={userId} job={jobId} reason=no slot to reserve");
                continue;
            }

            if (!hasReadyProfile)
                continue;

            if (_banManager.GetJobBans(userId)?.Contains(jobId) == true)
            {
                _sawmill.Warning($"Weekly forced role assignment failed: campaign={set.SetId} player={assignment.LastKnownCKey} userId={userId} job={jobId} reason=job ban");
                continue;
            }

            if (_playerManager.TryGetSessionById(userId, out var session) &&
                !IsWeeklyAccessAllowedForAssignment(session, set, assignment, true))
            {
                _sawmill.Warning($"Weekly forced role assignment failed: campaign={set.SetId} player={assignment.LastKnownCKey} userId={userId} job={jobId} reason=playtime gate");
                continue;
            }

            assigned[userId] = (jobId, station);
            _sawmill.Info($"Weekly forced role assigned: campaign={set.SetId} player={assignment.LastKnownCKey} userId={userId} job={jobId} station={ToPrettyString(station)}");
        }
    }

    public bool TryGetForcedLateJoinJob(
        ICommonSession session,
        EntityUid station,
        IReadOnlySet<ProtoId<JobPrototype>> restrictedRoles,
        [NotNullWhen(true)] out ProtoId<JobPrototype>? jobId,
        out string? message)
    {
        jobId = null;
        message = null;

        if (!TryGetActiveForcedAssignment(session.UserId, out var set, out var assignment))
            return false;

        if (_gameTicker.UserHasJoinedGame(session.UserId))
            return false;

        if (!TryParseForcedRoleAssignment(assignment, out _, out var forcedJob, out var error))
        {
            message = error;
            return false;
        }

        if (restrictedRoles.Contains(forcedJob) || _banManager.GetJobBans(session.UserId)?.Contains(forcedJob) == true)
        {
            message = $"Your reserved weekly role '{forcedJob}' is currently blocked.";
            _sawmill.Warning($"Weekly forced role assignment failed: campaign={set.SetId} player={assignment.LastKnownCKey} userId={session.UserId} job={forcedJob} reason=job blocked");
            return false;
        }

        if (!IsWeeklyAccessAllowedForAssignment(session, set, assignment, true))
        {
            message = $"Your reserved weekly role '{forcedJob}' requires more playtime.";
            return false;
        }

        if (!CanUseForcedLateJoinSlot(session.UserId, station, forcedJob, out message))
            return false;

        jobId = forcedJob;
        NotifyForcedRole(session, set, assignment, true);
        return true;
    }

    public bool CanLateJoinJob(ICommonSession session, EntityUid station, ProtoId<JobPrototype> jobId, [NotNullWhen(false)] out string? message)
    {
        message = null;

        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return true;

        if (!TryGetActiveForcedAssignment(session.UserId, out var set, out var ownAssignment))
        {
            if (IsJobFullyReservedForOthers(jobId))
            {
                message = $"{GetJobDisplayName(jobId)} is reserved by the active Weekly campaign.";
                return false;
            }

            return true;
        }

        if (!TryParseForcedRoleAssignment(ownAssignment, out _, out var forcedJob, out var error))
        {
            message = error;
            return false;
        }

        if (!string.Equals(forcedJob.Id, jobId.Id, StringComparison.Ordinal))
        {
            message = $"Your active Weekly campaign assignment is {GetJobDisplayName(forcedJob)}.";
            return false;
        }

        if (_banManager.GetJobBans(session.UserId)?.Contains(forcedJob) == true)
        {
            message = $"{GetJobDisplayName(forcedJob)} is blocked for your account.";
            return false;
        }

        if (!IsWeeklyAccessAllowedForAssignment(session, set, ownAssignment, true))
        {
            message = $"{GetJobDisplayName(forcedJob)} is reserved for you, but the Weekly playtime requirement is not met.";
            return false;
        }

        return CanUseForcedLateJoinSlot(session.UserId, station, forcedJob, out message);
    }

    public bool HasForcedPlaytimeBypass(NetUserId userId)
    {
        return TryGetActiveForcedAssignment(userId, out _, out var assignment) && assignment.BypassPlaytime;
    }

    public bool IsWeeklyAccessAllowedForJob(ICommonSession session, string? jobId, bool notify)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return true;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return true;

        if (IsWeeklyAccessAllowed(session, set, false))
            return true;

        if (jobId != null &&
            TryGetForcedAssignment(set, session.UserId, out var assignment) &&
            assignment.BypassPlaytime &&
            string.Equals(assignment.JobId, jobId, StringComparison.Ordinal))
        {
            return true;
        }

        if (notify)
            SendAccessDeniedNotice(session, set, false);

        return false;
    }

    private bool TryValidateForcedRoleJob(WeeklyModeSet set, string jobId, out string message)
    {
        if (!_prototype.TryIndex<JobPrototype>(jobId, out _))
        {
            message = $"Unknown job prototype '{jobId}'.";
            return false;
        }

        if (set.DefaultDisabledJobs.Contains(jobId))
        {
            message = $"Cannot force role {jobId}: campaign role is disabled.";
            return false;
        }

        if (set.DefaultRoleLimits.TryGetValue(jobId, out var limit) && limit == 0)
        {
            message = $"Cannot force role {jobId}:\ncampaign role limit is 0.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool IsForcedAssignmentFor(WeeklyForcedRoleAssignment assignment, NetUserId userId)
    {
        return Guid.TryParse(assignment.PlayerNetUserId, out var guid) && guid == userId.UserId;
    }

    private static bool TryParseForcedRoleAssignment(
        WeeklyForcedRoleAssignment assignment,
        out NetUserId userId,
        out ProtoId<JobPrototype> jobId,
        out string message)
    {
        jobId = new ProtoId<JobPrototype>(assignment.JobId);
        if (!Guid.TryParse(assignment.PlayerNetUserId, out var guid))
        {
            userId = new NetUserId(Guid.Empty);
            message = $"invalid playerNetUserId '{assignment.PlayerNetUserId}'";
            return false;
        }

        if (string.IsNullOrWhiteSpace(assignment.JobId))
        {
            userId = new NetUserId(guid);
            message = "empty forced jobId";
            return false;
        }

        userId = new NetUserId(guid);
        message = string.Empty;
        return true;
    }

    private static void SortForcedRoleAssignments(WeeklyModeSet set)
    {
        set.ForcedRoleAssignments.Sort((a, b) =>
        {
            var name = string.Compare(a.LastKnownCKey, b.LastKnownCKey, StringComparison.OrdinalIgnoreCase);
            return name != 0
                ? name
                : string.Compare(a.PlayerNetUserId, b.PlayerNetUserId, StringComparison.Ordinal);
        });
    }

    private string FormatForcedRoleAssignment(WeeklyForcedRoleAssignment assignment)
    {
        var roleName = _prototype.TryIndex<JobPrototype>(assignment.JobId, out var job)
            ? GetRoleDisplayName(job.ID, job.LocalizedName)
            : assignment.JobId;

        return $"{assignment.LastKnownCKey} [{assignment.PlayerNetUserId}] -> {assignment.JobId} ({roleName}) bypassPlaytime={assignment.BypassPlaytime} createdBy={assignment.CreatedBy} createdAt={assignment.CreatedAt:O}";
    }

    private bool TryGetActiveForcedAssignment(
        NetUserId userId,
        [NotNullWhen(true)] out WeeklyModeSet? set,
        [NotNullWhen(true)] out WeeklyForcedRoleAssignment? assignment)
    {
        set = null;
        assignment = null;
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return false;

        if (!_store.TryLoadSet(_state.ActiveSetId, out set))
            return false;

        return TryGetForcedAssignment(set, userId, out assignment);
    }

    private static bool TryGetForcedAssignment(
        WeeklyModeSet set,
        NetUserId userId,
        [NotNullWhen(true)] out WeeklyForcedRoleAssignment? assignment)
    {
        assignment = set.ForcedRoleAssignments.FirstOrDefault(assignment => IsForcedAssignmentFor(assignment, userId));
        return assignment != null;
    }

    private bool TryReserveForcedRoundStartSlot(
        IReadOnlyList<EntityUid> stations,
        Dictionary<EntityUid, Dictionary<ProtoId<JobPrototype>, int?>> stationJobs,
        ProtoId<JobPrototype> jobId,
        out EntityUid station)
    {
        station = EntityUid.Invalid;
        foreach (var candidate in stations)
        {
            if (!stationJobs.TryGetValue(candidate, out var jobs) ||
                !jobs.TryGetValue(jobId, out var slots))
            {
                continue;
            }

            station = candidate;
            if (slots == null)
                return true;

            if (slots <= 0)
                continue;

            jobs[jobId] = slots.Value - 1;
            return true;
        }

        station = EntityUid.Invalid;
        return false;
    }

    private bool CanUseForcedLateJoinSlot(NetUserId userId, EntityUid station, ProtoId<JobPrototype> jobId, [NotNullWhen(false)] out string? message)
    {
        message = null;
        if (station == EntityUid.Invalid)
            return true;

        if (!_stationJobs.TryGetJobSlot(station, jobId, out var slots))
        {
            message = $"{GetJobDisplayName(jobId)} is not available on the selected station.";
            return false;
        }

        if (slots == null)
            return true;

        if (slots.Value > 0)
            return true;

        if (TryGetActiveForcedAssignment(userId, out _, out var assignment) &&
            string.Equals(assignment.JobId, jobId.Id, StringComparison.Ordinal))
        {
            message = $"{GetJobDisplayName(jobId)} is reserved for you, but no station slot is currently available.";
            return false;
        }

        message = $"{GetJobDisplayName(jobId)} has no available slots.";
        return false;
    }

    private bool IsJobFullyReservedForOthers(ProtoId<JobPrototype> jobId)
    {
        var set = LoadActiveSetOrNull();
        if (set == null)
            return false;

        var reservations = CountUnsatisfiedForcedReservations(set, jobId);
        if (reservations <= 0)
            return false;

        var availableSlots = CountCurrentAvailableSlots(jobId);
        return availableSlots != null && availableSlots.Value <= reservations;
    }

    private int CountUnsatisfiedForcedReservations(WeeklyModeSet set, ProtoId<JobPrototype> jobId)
    {
        var count = 0;
        foreach (var assignment in set.ForcedRoleAssignments)
        {
            if (!string.Equals(assignment.JobId, jobId.Id, StringComparison.Ordinal))
                continue;

            if (!TryParseForcedRoleAssignment(assignment, out var userId, out _, out _))
                continue;

            if (_gameTicker.UserHasJoinedGame(userId))
                continue;

            count++;
        }

        return count;
    }

    private int? CountCurrentAvailableSlots(ProtoId<JobPrototype> jobId)
    {
        var total = 0;
        var sawFinite = false;
        var query = EntityQueryEnumerator<StationJobsComponent>();
        while (query.MoveNext(out var station, out _))
        {
            if (!_stationJobs.TryGetJobSlot(station, jobId, out var slots))
                continue;

            if (slots == null)
                return null;

            sawFinite = true;
            total += slots.Value;
        }

        return sawFinite ? total : 0;
    }

    private bool IsWeeklyAccessAllowedForAssignment(
        ICommonSession session,
        WeeklyModeSet set,
        WeeklyForcedRoleAssignment assignment,
        bool notify)
    {
        if (assignment.BypassPlaytime)
            return true;

        return IsWeeklyAccessAllowed(session, set, notify);
    }

    private void NotifyForcedRole(ICommonSession session, WeeklyModeSet set, WeeklyForcedRoleAssignment assignment, bool lateJoin)
    {
        if (_forcedRoleNoticeCooldowns.TryGetValue(session.UserId, out var nextAllowed) &&
            _timing.CurTime < nextAllowed)
        {
            return;
        }

        _forcedRoleNoticeCooldowns[session.UserId] = _timing.CurTime + TimeSpan.FromSeconds(30);
        var roleName = GetJobDisplayName(new ProtoId<JobPrototype>(assignment.JobId));
        var text = lateJoin
            ? $"Для вас зарезервирована роль: {roleName}."
            : $"Для текущей Weekly-кампании вам гарантированно назначена роль:\n\n{roleName}\n\nЭта роль зарезервирована за вашим аккаунтом и будет выдана независимо от настроек приоритетов ролей.";

        _chatManager.DispatchServerMessage(session, text);
    }
}
