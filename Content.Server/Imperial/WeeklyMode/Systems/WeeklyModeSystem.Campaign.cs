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
    public bool TryCreateSet(string setId, string mapPath, string? displayName, out string message)
    {
        if (!WeeklyModeStore.IsSafeId(setId))
        {
            message = "Invalid setId. Use only ASCII letters, digits, '-', '_' or '.'.";
            return false;
        }

        if (!TryResolveBaseMapPath(mapPath, out var baseMap, out var resolvedMapPath, out message))
            return false;

        if (_store.TryLoadSet(setId, out _))
        {
            message = $"Weekly set '{setId}' already exists.";
            return false;
        }

        var autosaveMinutes = _cfg.GetCVar(ICCVars.WeeklyModeDefaultAutosaveMinutes);
        var retainAutosaves = _cfg.GetCVar(ICCVars.WeeklyModeDefaultRetainAutosaves);
        var set = _store.CreateSet(setId, baseMap.ID, autosaveMinutes, retainAutosaves, displayName, resolvedMapPath.CanonPath);
        message = $"Created weekly set '{set.SetId}' with base map path '{set.BaseMapPath}' using map prototype '{set.BaseMapPrototype}'.";
        _sawmill.Info(message);
        return true;
    }

    public bool TrySetMap(string setId, string mapPath, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (!TryResolveBaseMapPath(mapPath, out var baseMap, out var resolvedMapPath, out message))
            return false;

        set.BaseMapPrototype = baseMap.ID;
        set.BaseMapPath = resolvedMapPath.CanonPath;
        _store.SaveSet(set);
        message = $"Weekly set '{set.SetId}' map changed to '{set.BaseMapPath}' using map prototype '{set.BaseMapPrototype}'.";
        return true;
    }

    public string ListSets()
    {
        var ids = _store.ListSetIds().OrderBy(id => id).ToArray();
        if (ids.Length == 0)
            return "No weekly mode sets exist.";

        var lines = new List<string> { "Weekly mode sets:" };
        foreach (var id in ids)
        {
            if (!_store.TryLoadSet(id, out var set))
                continue;

            lines.Add($"- {set.SetId}: base={DescribeBaseMap(set)}, current={set.CurrentSnapshot ?? "<base>"}, autosave={set.AutosaveMinutes}m, warning={set.AutosaveWarningMinutes}m, retain={set.RetainAutosaves}");
        }

        return string.Join('\n', lines);
    }

    public string GetStatus()
    {
        return GetStatus(null);
    }

    public string GetStatus(string? setId)
    {
        if (!_state.IsActive)
        {
            if (setId == null)
                return $"Weekly mode is inactive. Data root: {_store.Root}";

            return DescribeSetStatus(setId);
        }

        var next = _nextAutosaveAt == null
            ? "not scheduled"
            : $"{Math.Max(0, (_nextAutosaveAt.Value - _timing.CurTime).TotalSeconds):F0}s";

        var active = $"Weekly mode active: set={_state.ActiveSetId}, snapshot={_state.ActiveSnapshotId ?? "<base>"}, pending={_state.PendingSnapshotId ?? "<none>"}, next autosave={next}, data root={_store.Root}";
        return setId == null ? active : $"{active}\n{DescribeSetStatus(setId)}";
    }

    private string DescribeSetStatus(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        return $"Set '{set.SetId}': base={DescribeBaseMap(set)}, current={set.CurrentSnapshot ?? "<base>"}, disabled=[{string.Join(", ", set.DefaultDisabledJobs)}], aliases={set.DefaultRoleAliases.Count}, limits={set.DefaultRoleLimits.Count}, snapshots={set.Snapshots.Count}.";
    }

    public string ListSnapshots(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        if (set.Snapshots.Count == 0)
            return $"Weekly set '{set.SetId}' has no snapshots.";

        var now = DateTime.UtcNow;
        var contentVersion = _cfg.GetCVar(Robust.Shared.CVars.BuildVersion);
        var engineVersion = _cfg.GetCVar(Robust.Shared.CVars.BuildEngineVersion);
        var lines = new List<string> { $"Snapshots for weekly set '{set.SetId}':" };

        foreach (var snapshotId in set.Snapshots.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!_store.TryLoadSnapshot(set.SetId, snapshotId, out var metadata))
            {
                lines.Add($"- {snapshotId}: unreadable or incomplete");
                continue;
            }

            var compatible = metadata.SchemaVersion == WeeklyModeSet.CurrentSchemaVersion &&
                             metadata.ContentVersion == contentVersion &&
                             metadata.EngineVersion == engineVersion;
            metadata.CompatibleWithBuild = compatible;
            var age = now - metadata.CreatedAtUtc;
            var size = metadata.BundleSizeBytes > 0
                ? metadata.BundleSizeBytes
                : _store.GetDirectorySize(_store.SnapshotDirectory(set.SetId, snapshotId));
            var current = snapshotId == set.CurrentSnapshot ? " current" : string.Empty;
            var savedMapName = string.IsNullOrWhiteSpace(metadata.SavedMapName)
                ? "<unknown>"
                : metadata.SavedMapName;
            var savedMapId = metadata.SavedMapId?.ToString(CultureInfo.InvariantCulture) ?? "<unknown>";
            lines.Add($"- {snapshotId}{current}: kind={metadata.Kind}, utc={metadata.CreatedAtUtc:O}, age={FormatAge(age)}, size={size}B, compatible={compatible}, map={savedMapName}, mapId={savedMapId}, entities={metadata.EntityCount}, by={metadata.CreatedBy}, note=\"{metadata.Notes}\"");
        }

        return string.Join('\n', lines);
    }

    public bool TryDeleteSnapshot(string setId, string snapshotId, out string message)
    {
        if (!TryLoadMutableSet(setId, out var set, out message))
            return false;

        if (!WeeklyModeStore.IsSafeId(snapshotId))
        {
            message = "Invalid snapshotId. Use only ASCII letters, digits, '-', '_' or '.'.";
            return false;
        }

        if (set.CurrentSnapshot == snapshotId)
        {
            message = $"Snapshot '{snapshotId}' is current for set '{set.SetId}' and cannot be deleted.";
            return false;
        }

        if (!_store.SnapshotExists(set.SetId, snapshotId) && !set.Snapshots.Contains(snapshotId))
        {
            message = $"Snapshot '{snapshotId}' was not found in set '{set.SetId}'.";
            return false;
        }

        _store.DeleteSnapshot(set.SetId, snapshotId);
        set.Snapshots.Remove(snapshotId);
        _store.SaveSet(set);
        message = $"Deleted snapshot '{snapshotId}' from set '{set.SetId}'.";
        _sawmill.Warning(message);
        return true;
    }

    public bool TryStart(string setId, string? snapshotId, string startedBy, out string message)
    {
        if (!EnsureEnabled(out message))
            return false;

        if (!TryEnterOperation(out message))
            return false;

        try
        {
            if (!_store.TryLoadSet(setId, out var set))
            {
                message = $"Weekly set '{setId}' was not found.";
                return false;
            }

            if (!TryValidateSetConfig(set, out var validationErrors))
            {
                message = $"Weekly set '{set.SetId}' config is invalid:\n- {string.Join("\n- ", validationErrors)}";
                return false;
            }

            var selectedSnapshot = snapshotId ?? set.CurrentSnapshot;
            if (selectedSnapshot != null &&
                !TryLoadCompatibleSnapshot(set, selectedSnapshot, out _, out message))
            {
                return false;
            }

            _state.IsActive = true;
            _state.ActiveSetId = set.SetId;
            _state.PendingSnapshotId = selectedSnapshot;
            _state.ActiveSnapshotId = selectedSnapshot;
            _state.StartedAtUtc = DateTime.UtcNow;
            _state.StartedBy = startedBy;
            ClearActiveWeeklyMapTracking();
            _store.SaveState(_state);

            RestartForWeeklyMap();

            message = selectedSnapshot == null
                ? $"Weekly set '{set.SetId}' armed. The next round will start from base map '{set.BaseMapPrototype}'."
                : $"Weekly set '{set.SetId}' armed. The next round will start from snapshot '{selectedSnapshot}'.";
            _sawmill.Info($"{message} Operator: {startedBy}");
            return true;
        }
        finally
        {
            _operationInProgress = false;
        }
    }

    public bool TryRollback(string setId, string snapshotId, string startedBy, out string message)
    {
        if (!EnsureEnabled(out message))
            return false;

        if (!WeeklyModeStore.IsSafeId(snapshotId))
        {
            message = "Invalid snapshotId. Use only ASCII letters, digits, '-', '_' or '.'.";
            return false;
        }

        if (!TryEnterOperation(out message))
            return false;

        try
        {
            if (!_store.TryLoadSet(setId, out var set))
            {
                message = $"Weekly set '{setId}' was not found.";
                return false;
            }

            if (!TryLoadCompatibleSnapshot(set, snapshotId, out var targetMetadata, out message))
                return false;

            if (targetMetadata.SchemaVersion != WeeklyModeSet.CurrentSchemaVersion)
            {
                message = $"Snapshot '{snapshotId}' schemaVersion {targetMetadata.SchemaVersion} is not compatible with weekly schema {WeeklyModeSet.CurrentSchemaVersion}.";
                return false;
            }

            if (!TrySaveSnapshotInternal(
                    set.SetId,
                    WeeklySnapshotKind.RollbackBackup,
                    $"rollback backup before restoring {snapshotId}",
                    startedBy,
                    false,
                    out var backupId,
                    out var backupMessage))
            {
                message = $"Rollback aborted because backup failed: {backupMessage}";
                return false;
            }

            set.CurrentSnapshot = snapshotId;
            _store.SaveSet(set);

            _state.IsActive = true;
            _state.ActiveSetId = set.SetId;
            _state.PendingSnapshotId = snapshotId;
            _state.ActiveSnapshotId = snapshotId;
            _state.StartedAtUtc ??= DateTime.UtcNow;
            _state.StartedBy = startedBy;
            ClearActiveWeeklyMapTracking();
            _store.SaveState(_state);

            RestartForWeeklyMap();

            _chatManager.DispatchServerAnnouncement($"Weekly Mode rollback is restarting the round into snapshot '{snapshotId}'. Backup snapshot: '{backupId}'.");

            message = $"Weekly rollback armed for set '{set.SetId}' snapshot '{snapshotId}'. Backup '{backupId}' was created and the round is restarting into the selected snapshot.";
            _sawmill.Warning($"{message} Operator: {startedBy}");
            return true;
        }
        finally
        {
            _operationInProgress = false;
        }
    }

    public bool TryPrepareRollback(string setId, string snapshotId, string requestedBy, out string token, out string message)
    {
        token = string.Empty;
        if (!EnsureEnabled(out message))
            return false;

        if (!WeeklyModeStore.IsSafeId(snapshotId))
        {
            message = "Invalid snapshotId. Use only ASCII letters, digits, '-', '_' or '.'.";
            return false;
        }

        if (!_store.TryLoadSet(setId, out var set))
        {
            message = $"Weekly set '{setId}' was not found.";
            return false;
        }

        if (!TryLoadCompatibleSnapshot(set, snapshotId, out var metadata, out message))
            return false;

        token = Guid.NewGuid().ToString("N")[..8];
        var expires = DateTime.UtcNow.AddMinutes(2);
        _rollbackConfirmations[token] = new RollbackConfirmation(set.SetId, snapshotId, expires, requestedBy);
        message = $"Rollback confirmation token: {token}\nSnapshot: {metadata.SnapshotId} ({metadata.Kind}, {metadata.CreatedAtUtc:O}, by {metadata.CreatedBy})\nThis will restart the round. Confirm within 2 minutes with: wm.rollback {set.SetId} {snapshotId} {token}";
        _sawmill.Warning($"Rollback token {token} prepared for set '{set.SetId}' snapshot '{snapshotId}' by {requestedBy}.");
        return true;
    }

    public bool TryConfirmRollback(string setId, string snapshotId, string token, string startedBy, out string message)
    {
        if (!_rollbackConfirmations.TryGetValue(token, out var confirmation) ||
            confirmation.SetId != setId ||
            confirmation.SnapshotId != snapshotId)
        {
            message = "Invalid rollback confirmation token.";
            return false;
        }

        if (confirmation.ExpiresAtUtc < DateTime.UtcNow)
        {
            _rollbackConfirmations.Remove(token);
            message = "Rollback confirmation token has expired.";
            return false;
        }

        _rollbackConfirmations.Remove(token);
        return TryRollback(setId, snapshotId, startedBy, out message);
    }

    public bool TryCancel(out string message)
    {
        if (!TryEnterOperation(out message))
            return false;

        try
        {
            DeactivateWeeklyMode();
            message = "Weekly mode cancelled. Future rounds will use normal map selection.";
            _sawmill.Info(message);
            return true;
        }
        finally
        {
            _operationInProgress = false;
        }
    }

    public bool TryStop(string setId, out string message)
    {
        if (!WeeklyModeStore.IsSafeId(setId))
        {
            message = "Invalid setId. Use only ASCII letters, digits, '-', '_' or '.'.";
            return false;
        }

        if (!TryEnterOperation(out message))
            return false;

        try
        {
            if (!_state.IsActive || _state.ActiveSetId == null)
            {
                message = "Weekly mode is not active.";
                return false;
            }

            if (!string.Equals(_state.ActiveSetId, setId, StringComparison.Ordinal))
            {
                message = $"Weekly set '{setId}' is not active. Active set: '{_state.ActiveSetId}'.";
                return false;
            }

            DeactivateWeeklyMode();
            message = $"Weekly set '{setId}' stopped. Autosave and runtime role overrides were cleared; snapshots and campaign config were kept.";
            _sawmill.Info(message);
            return true;
        }
        finally
        {
            _operationInProgress = false;
        }
    }

    public bool TrySaveSnapshot(string setId, WeeklySnapshotKind kind, string notes, string createdBy, out string message)
    {
        if (!EnsureEnabled(out message))
            return false;

        if (!TryEnterOperation(out message))
            return false;

        try
        {
            return TrySaveSnapshotInternal(setId, kind, notes, createdBy, true, out _, out message);
        }
        finally
        {
            _operationInProgress = false;
        }
    }

    private bool TrySaveSnapshotInternal(
        string setId,
        WeeklySnapshotKind kind,
        string notes,
        string createdBy,
        bool makeCurrent,
        [NotNullWhen(true)] out string? snapshotId,
        out string message)
    {
        snapshotId = null;

        if (!_store.TryLoadSet(setId, out var set))
        {
            message = $"Weekly set '{setId}' was not found.";
            return false;
        }

        if (!IsActiveSet(set.SetId))
        {
            message = $"Weekly set '{set.SetId}' is not the active weekly set.";
            return false;
        }

        if (!TryResolveActiveWeeklyMap(
                set,
                out var mapId,
                out var mapEntity,
                out var mapName,
                out var gridUids,
                out var activeEntityCount,
                out message))
        {
            return false;
        }

        _sawmill.Info(
            "Weekly save:\n" +
            $"set={set.SetId}\n" +
            $"activeMapId={(int) mapId}\n" +
            $"activeMapEntity={mapEntity.Id}\n" +
            $"activeMapName={mapName}\n" +
            $"baseMapPrototype={set.BaseMapPrototype}\n" +
            $"baseMapPath={set.BaseMapPath}\n" +
            $"grids={string.Join(",", gridUids.Select(uid => uid.Id))}\n" +
            $"entities={activeEntityCount}");

        snapshotId = BuildSnapshotId(kind);
        var tempDirectory = _store.TempSnapshotDirectory(set.SetId, snapshotId);
        var stationPath = _store.SnapshotFilePath(tempDirectory, WeeklyModeStore.StationFileName);

        _resource.UserData.Delete(tempDirectory);
        _resource.UserData.CreateDir(tempDirectory);

        var snapshotExclusions = CollectSnapshotExclusions(set);
        _sawmill.Info(
            "Weekly snapshot persistence:\n" +
            $"snapshot={snapshotId}\n" +
            $"persistAutonomousMobs={set.PersistAutonomousMobs}\n" +
            $"autonomousMobsIncluded={snapshotExclusions.AutonomousMobsIncluded}\n" +
            $"autonomousMobsExcluded={snapshotExclusions.AutonomousMobsExcluded}\n" +
            $"playerBodiesExcluded={snapshotExclusions.PlayerBodiesExcluded}\n" +
            $"borgChassisIncluded={snapshotExclusions.BorgChassisIncluded}\n" +
            $"borgChassisExcluded={snapshotExclusions.BorgChassisExcluded}");

        if (!TrySaveMapExcludingPlayers(mapId, stationPath, snapshotExclusions, out var yamlUidMap))
        {
            _resource.UserData.Delete(tempDirectory);
            message = $"Failed to serialize map {mapId} for snapshot '{snapshotId}'.";
            return false;
        }

        if (!TrySanitizeSerializedSnapshot(stationPath, out var sanitization, out message))
        {
            _resource.UserData.Delete(tempDirectory);
            return false;
        }

        if (sanitization.Changed)
        {
            _sawmill.Info(
                "Weekly snapshot sanitation:\n" +
                $"snapshot={snapshotId}\n" +
                $"removedStationMembers={sanitization.RemovedStationMembers}\n" +
                $"removedSuitSensorEntityReferences={sanitization.RemovedSuitSensorEntityReferences}\n" +
                $"removedInvalidContainerReferences={sanitization.RemovedInvalidContainerReferences}\n" +
                $"staleBuckleReferencesRemoved={sanitization.RemovedStaleBuckleReferences}\n" +
                $"staleStrapReferencesRemoved={sanitization.RemovedStaleStrapReferences}\n" +
                $"staleJointReferencesRemoved={sanitization.RemovedStaleJointReferences}\n" +
                $"borgChassisDisabled={sanitization.DisabledBorgChassis}\n" +
                $"resetMapInitializationFields={sanitization.ResetMapInitializationFields}\n" +
                $"resetMindContainers={sanitization.ResetMindContainers}\n" +
                $"suppressedMapInitOnlyComponents={sanitization.SuppressedMapInitOnlyComponents}\n" +
                $"suppressedStartingItems={sanitization.SuppressedStartingItems}");
        }

        var patch = BuildContainerPatch(yamlUidMap, snapshotExclusions.ExcludedRoots);
        var roleOverrides = new WeeklyRoleOverrides
        {
            DisabledJobs = set.DefaultDisabledJobs.ToList(),
            RoleAliases = new Dictionary<string, string>(set.DefaultRoleAliases),
            RoleLimits = new Dictionary<string, int>(set.DefaultRoleLimits),
        };

        var metadata = new WeeklySnapshotMetadata
        {
            CompatibleWithBuild = true,
            SnapshotId = snapshotId,
            SetId = set.SetId,
            Kind = kind,
            BaseMapPrototype = set.BaseMapPrototype,
            BaseMapPath = set.BaseMapPath,
            CreatedAtUtc = DateTime.UtcNow,
            ContentVersion = _cfg.GetCVar(Robust.Shared.CVars.BuildVersion),
            EngineVersion = _cfg.GetCVar(Robust.Shared.CVars.BuildEngineVersion),
            Notes = notes,
            CreatedBy = createdBy,
            SavedMapId = (int) mapId,
            SavedMapName = mapName,
            SavedGridIds = gridUids.Select(uid => uid.Id).OrderBy(id => id).ToList(),
            EntityCount = yamlUidMap.Count,
        };

        _store.SaveRoleOverrides(tempDirectory, roleOverrides);
        _store.SaveContainerPatch(tempDirectory, patch);
        _store.SaveSnapshotMetadata(tempDirectory, metadata);

        metadata.BundleSizeBytes = _store.GetDirectorySize(tempDirectory);
        _store.SaveSnapshotMetadata(tempDirectory, metadata);

        var integrity = BuildIntegrity(set.SetId, snapshotId, metadata.CreatedAtUtc, tempDirectory);
        _store.SaveSnapshotIntegrity(tempDirectory, integrity);

        if (!IsCompleteSnapshotDirectory(tempDirectory, out message))
        {
            _resource.UserData.Delete(tempDirectory);
            return false;
        }

        metadata.BundleSizeBytes = _store.GetDirectorySize(tempDirectory);
        _store.SaveSnapshotMetadata(tempDirectory, metadata);
        integrity = BuildIntegrity(set.SetId, snapshotId, metadata.CreatedAtUtc, tempDirectory);
        _store.SaveSnapshotIntegrity(tempDirectory, integrity);

        _store.ReplaceSnapshotDirectory(tempDirectory, set.SetId, snapshotId);

        if (!set.Snapshots.Contains(snapshotId))
            set.Snapshots.Add(snapshotId);

        if (makeCurrent)
        {
            set.CurrentSnapshot = snapshotId;
            _state.ActiveSnapshotId = snapshotId;
            _store.SaveState(_state);
        }

        PruneAutosaves(set);
        _store.SaveSet(set);

        message = $"Saved weekly snapshot '{snapshotId}' for set '{set.SetId}' to {_store.SnapshotDirectory(set.SetId, snapshotId)}.";
        _sawmill.Info($"{message} Operator: {createdBy}");
        return true;
    }
}
