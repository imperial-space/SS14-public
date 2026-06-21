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

public sealed class WeeklyModeSystem : EntitySystem
{
    private const int WeeklyCargoBoxedCapacity = 30;

    [Flags]
    private enum WeeklyLiveConfigChange
    {
        None = 0,
        Research = 1,
        Cargo = 2,
        Recipes = 4,
        Roles = 8,
    }

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameMapManager _gameMapManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceManager _resource = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly ContainerSystem _containers = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly PoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly LightReplacerSystem _lightReplacer = default!;
    [Dependency] private readonly ActionGrantSystem _actionGrant = default!;
    [Dependency] private readonly BinSystem _bin = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTime = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly SharedBorgSystem _borg = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IDependencyCollection _dependency = default!;

    private readonly WeeklyModeRuntimeState _state = new();
    private readonly Dictionary<EntityUid, Dictionary<string, int?>> _originalSlots = new();
    private readonly Dictionary<string, RollbackConfirmation> _rollbackConfirmations = new();
    private readonly HashSet<NetUserId> _accessLobbyNoticeSent = new();
    private readonly Dictionary<NetUserId, TimeSpan> _accessNoticeCooldowns = new();
    private readonly Dictionary<NetUserId, TimeSpan> _forcedRoleNoticeCooldowns = new();

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<ContainerManagerComponent> _containerQuery;
    private EntityQuery<YamlUidComponent> _yamlUidQuery;
    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<MindComponent> _mindQuery;
    private EntityQuery<MindContainerComponent> _mindContainerQuery;
    private WeeklyModeStore _store = default!;
    private ISawmill _sawmill = default!;
    private bool _enabled;
    private bool _operationInProgress;
    private TimeSpan? _nextAutosaveAt;
    private bool _autosaveWarningIssued;
    private MapId? _activeWeeklyMapId;
    private EntityUid? _activeWeeklyMapEntity;
    private readonly List<EntityUid> _activeWeeklyGridIds = new();
    private string? _activeWeeklyMapPrototype;
    private string? _activeWeeklyMapName;

    private sealed record RollbackConfirmation(string SetId, string SnapshotId, DateTime ExpiresAtUtc, string RequestedBy);
    internal readonly record struct SnapshotSanitizationResult(
        int RemovedStationMembers,
        int RemovedSuitSensorEntityReferences,
        int RemovedInvalidContainerReferences,
        int RemovedStaleBuckleReferences,
        int RemovedStaleStrapReferences,
        int RemovedStaleJointReferences,
        int DisabledBorgChassis,
        int ResetMapInitializationFields,
        int ResetMindContainers,
        int SuppressedMapInitOnlyComponents,
        int SuppressedStartingItems)
    {
        public bool Changed => RemovedStationMembers > 0 ||
                               RemovedSuitSensorEntityReferences > 0 ||
                               RemovedInvalidContainerReferences > 0 ||
                               RemovedStaleBuckleReferences > 0 ||
                               RemovedStaleStrapReferences > 0 ||
                               RemovedStaleJointReferences > 0 ||
                               DisabledBorgChassis > 0 ||
                               ResetMapInitializationFields > 0 ||
                               ResetMindContainers > 0 ||
                               SuppressedMapInitOnlyComponents > 0 ||
                               SuppressedStartingItems > 0;
    }

    internal readonly record struct SnapshotExclusionResult(
        HashSet<EntityUid> ExcludedRoots,
        HashSet<string> ForceMapSavablePrototypes,
        int AutonomousMobsIncluded,
        int AutonomousMobsExcluded,
        int PlayerBodiesExcluded,
        int BorgChassisIncluded,
        int BorgChassisExcluded);

    private static readonly string[] SnapshotMapInitOnlyComponents =
    {
        "ConditionalSpawner",
        "ContainerFill",
        "EntityTableSpawner",
        "EntityTableContainerFill",
        "RandomDecalSpawner",
        "RandomSpawner",
        "StorageFill",
        "RandomFillSolution",
    };

    private static readonly HashSet<string> WeeklyTechnologyBranches = new(StringComparer.Ordinal)
    {
        "industrial",
        "arsenal",
        "experimental",
        "service",
    };

    private static readonly Dictionary<string, string> WeeklyTechnologyDisciplineIds = new(StringComparer.Ordinal)
    {
        ["industrial"] = "Industrial",
        ["arsenal"] = "Arsenal",
        ["experimental"] = "Experimental",
        ["service"] = "CivilianServices",
    };

    private sealed record WeeklyRecipeTargetAlias(string[] Entities, string[] Packs, bool All = false);

    private static readonly Dictionary<string, WeeklyRecipeTargetAlias> WeeklyRecipeTargetAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["protolathe"] = new(new[] { "Protolathe", "ProtolatheHyperConvection" }, Array.Empty<string>()),
        ["security"] = new(new[] { "SecurityTechFab", "AmmoTechFab" }, Array.Empty<string>()),
        ["medical"] = new(new[] { "MedicalTechFab" }, Array.Empty<string>()),
        ["engineering"] = new(new[] { "Protolathe", "ProtolatheHyperConvection", "CircuitImprinter", "CircuitImprinterHyperConvection" }, Array.Empty<string>()),
        ["service"] = new(new[] { "Autolathe", "AutolatheHyperConvection", "Protolathe", "ProtolatheHyperConvection", "CircuitImprinter", "CircuitImprinterHyperConvection", "UniformPrinter" }, Array.Empty<string>()),
        ["science"] = new(new[] { "Protolathe", "ProtolatheHyperConvection", "CircuitImprinter", "CircuitImprinterHyperConvection", "ExosuitFabricator" }, Array.Empty<string>()),
        ["cargo"] = new(new[] { "Autolathe", "AutolatheHyperConvection", "CircuitImprinter", "CircuitImprinterHyperConvection" }, Array.Empty<string>()),
        ["civilian"] = new(new[] { "Autolathe", "AutolatheHyperConvection", "UniformPrinter" }, Array.Empty<string>()),
        ["all"] = new(Array.Empty<string>(), Array.Empty<string>(), true),
    };

    public override void Initialize()
    {
        base.Initialize();

        _xformQuery = GetEntityQuery<TransformComponent>();
        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _containerQuery = GetEntityQuery<ContainerManagerComponent>();
        _yamlUidQuery = GetEntityQuery<YamlUidComponent>();
        _actorQuery = GetEntityQuery<ActorComponent>();
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _mindQuery = GetEntityQuery<MindComponent>();
        _mindContainerQuery = GetEntityQuery<MindContainerComponent>();
        _sawmill = _logManager.GetSawmill("weekly-mode");
        RebuildStore(_cfg.GetCVar(CCVars.WeeklyModeDataRoot));

        _enabled = _cfg.GetCVar(CCVars.WeeklyModeEnabled);
        Subs.CVar(_cfg, CCVars.WeeklyModeEnabled, value => _enabled = value, true);
        Subs.CVar(_cfg, CCVars.WeeklyModeDataRoot, RebuildStore, true);

        if (_store.TryLoadState(out var state))
            CopyState(state, _state);

        SubscribeLocalEvent<LoadingMapsEvent>(OnLoadingMaps);
        SubscribeLocalEvent<PreGameMapLoad>(OnPreGameMapLoad);
        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialized);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<TechnologyDatabaseComponent, ComponentStartup>(OnTechnologyDatabaseStartup);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        SubscribeLocalEvent<StationJobsGetCandidatesEvent>(OnStationJobsGetCandidates);
        SubscribeLocalEvent<IsRoleAllowedEvent>(OnIsRoleAllowed);
        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnGetDisallowedJobs);
    }

    public override void Update(float frameTime)
    {
        if (!_enabled ||
            !_state.IsActive ||
            _operationInProgress ||
            _state.ActiveSetId == null ||
            _gameTicker.RunLevel != GameRunLevel.InRound ||
            _nextAutosaveAt == null)
        {
            return;
        }

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
        {
            _sawmill.Error($"Weekly autosave skipped: active set '{_state.ActiveSetId}' is missing.");
            _nextAutosaveAt = _timing.CurTime + TimeSpan.FromMinutes(1);
            _autosaveWarningIssued = false;
            return;
        }

        var timeUntilSave = _nextAutosaveAt.Value - _timing.CurTime;
        if (!_autosaveWarningIssued &&
            set.AutosaveWarningMinutes > 0 &&
            timeUntilSave > TimeSpan.Zero &&
            timeUntilSave <= TimeSpan.FromMinutes(set.AutosaveWarningMinutes))
        {
            SendAutosaveOoc(
                $"Внимание! Через {set.AutosaveWarningMinutes} минуты будет выполнено автоматическое сохранение мира.\n\n" +
                "Во время сохранения возможна кратковременная задержка или подвисание игры.");
            _autosaveWarningIssued = true;
        }

        if (_timing.CurTime < _nextAutosaveAt.Value)
            return;

        SendAutosaveOoc("Начинается автоматическое сохранение мира. Возможна кратковременная задержка.");

        if (TrySaveSnapshot(set.SetId, WeeklySnapshotKind.Auto, "scheduled autosave", "server", out var message))
        {
            SendAutosaveOoc("Автоматическое сохранение мира завершено.");
        }
        else
        {
            _sawmill.Warning($"Weekly autosave failed: {message}");
            SendAutosaveOoc("Автоматическое сохранение мира завершилось ошибкой. Администраторы уведомлены.");
        }

        _nextAutosaveAt = _timing.CurTime + TimeSpan.FromMinutes(Math.Max(1, set.AutosaveMinutes));
        _autosaveWarningIssued = false;
    }

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

        var autosaveMinutes = _cfg.GetCVar(CCVars.WeeklyModeDefaultAutosaveMinutes);
        var retainAutosaves = _cfg.GetCVar(CCVars.WeeklyModeDefaultRetainAutosaves);
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

    public bool TrySetAutosave(string setId, int intervalMinutes, int warningMinutes, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (intervalMinutes < 1)
        {
            message = "Autosave interval must be at least 1 minute.";
            return false;
        }

        if (warningMinutes < 0)
        {
            message = "Autosave warning may not be negative.";
            return false;
        }

        if (warningMinutes >= intervalMinutes)
        {
            message = "Autosave warning must be shorter than the autosave interval.";
            return false;
        }

        set.AutosaveMinutes = intervalMinutes;
        set.AutosaveWarningMinutes = warningMinutes;
        _store.SaveSet(set);
        message = $"Autosave for weekly set '{set.SetId}' set to every {intervalMinutes} minutes with {warningMinutes} minute warning.";
        return true;
    }

    public bool TrySetPersistAutonomousMobs(string setId, bool enabled, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        set.PersistAutonomousMobs = enabled;
        _store.SaveSet(set);
        message = $"Autonomous mob persistence for weekly set '{set.SetId}' set to {enabled}.";
        return true;
    }

    public bool TryExcludeMobPrototype(string setId, string prototypeId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (!_prototype.TryIndex<EntityPrototype>(prototypeId, out _))
        {
            message = $"Unknown entity prototype '{prototypeId}'.";
            return false;
        }

        if (!set.ExcludedMobPrototypes.Contains(prototypeId, StringComparer.Ordinal))
            set.ExcludedMobPrototypes.Add(prototypeId);

        set.ExcludedMobPrototypes.Sort(StringComparer.Ordinal);
        _store.SaveSet(set);
        message = $"Excluded mob prototype '{prototypeId}' from weekly set '{set.SetId}'.";
        return true;
    }

    public string ListExcludedMobPrototypes(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        return set.ExcludedMobPrototypes.Count == 0
            ? $"Weekly set '{set.SetId}' has no excluded mob prototypes."
            : $"Excluded mob prototypes for weekly set '{set.SetId}':\n- {string.Join("\n- ", set.ExcludedMobPrototypes.OrderBy(x => x, StringComparer.Ordinal))}";
    }

    public bool TryClearExcludedMobPrototype(string setId, string prototypeId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (!set.ExcludedMobPrototypes.Remove(prototypeId))
        {
            message = $"Weekly set '{set.SetId}' had no excluded mob prototype '{prototypeId}'.";
            return true;
        }

        _store.SaveSet(set);
        message = $"Removed excluded mob prototype '{prototypeId}' from weekly set '{set.SetId}'.";
        return true;
    }

    public bool TrySetMinPlaytime(string setId, int hours, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (hours < 0)
        {
            message = "Minimum playtime may not be negative.";
            return false;
        }

        set.MinPlaytimeHours = hours;
        _store.SaveSet(set);
        message = $"Minimum server playtime for weekly set '{set.SetId}' set to {hours} hours.";
        return true;
    }

    public bool TrySetDiscordChannel(string setId, string channel, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        channel = channel.Trim().Trim('"');
        if (channel.Length > 128)
        {
            message = "Discord channel/link may not be longer than 128 characters.";
            return false;
        }

        if (channel.Any(char.IsControl))
        {
            message = "Discord channel/link may not contain control characters.";
            return false;
        }

        set.DiscordChannel = channel;
        _store.SaveSet(set);
        message = string.IsNullOrWhiteSpace(channel)
            ? $"Discord channel cleared for weekly set '{set.SetId}'."
            : $"Discord channel for weekly set '{set.SetId}' set to '{channel}'.";
        return true;
    }

    public string GetAccessStatus(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var discord = string.IsNullOrWhiteSpace(set.DiscordChannel) ? "<none>" : set.DiscordChannel;
        return $"Access for weekly set '{set.SetId}': minPlaytimeHours={set.MinPlaytimeHours}, discord={discord}.";
    }

    public bool TryClearAccess(string setId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        set.MinPlaytimeHours = 0;
        set.DiscordChannel = string.Empty;
        _store.SaveSet(set);
        message = $"Cleared access restrictions for weekly set '{set.SetId}'.";
        return true;
    }

    public bool TrySetRandomGameRules(string setId, bool enabled, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        set.RandomGameRulesEnabled = enabled;
        _store.SaveSet(set);
        message = $"Random gamerules for weekly set '{set.SetId}' set to {enabled}.";
        return true;
    }

    public string GetGameRulesStatus(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        return $"Gamerules for weekly set '{set.SetId}': randomGameRulesEnabled={set.RandomGameRulesEnabled}.";
    }

    public bool TryAddTechnology(
        string setId,
        string branch,
        string technologyId,
        int cost,
        int tier,
        IReadOnlyList<string> recipeIds,
        out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);

        if (!TryNormalizeTechnologyBranch(branch, out branch, out message))
            return false;

        technologyId = technologyId.Trim();
        if (!WeeklyModeStore.IsSafeId(technologyId))
        {
            message = "technologyId must contain only ASCII letters, digits, '-', '_' or '.'.";
            return false;
        }

        if (set.WeeklyTechnologies.Any(entry => string.Equals(entry.TechnologyId, technologyId, StringComparison.Ordinal)))
        {
            message = $"Technology '{technologyId}' already exists in weekly set '{set.SetId}'.";
            return false;
        }

        if (cost < 0)
        {
            message = "Technology cost may not be negative.";
            return false;
        }

        if (tier is < 1 or > 3)
        {
            message = "Technology tier must be 1, 2 or 3.";
            return false;
        }

        var recipeConfig = _store.LoadRecipesOrDefault(set.SetId);
        var weeklyRecipeIds = recipeConfig.Recipes.Select(recipe => recipe.Id).ToHashSet(StringComparer.Ordinal);
        var recipes = new List<string>();
        foreach (var rawRecipe in recipeIds)
        {
            var recipeId = rawRecipe.Trim();
            if (!_prototype.TryIndex<LatheRecipePrototype>(recipeId, out _) &&
                !weeklyRecipeIds.Contains(recipeId))
            {
                message = $"Unknown lathe or weekly recipe '{recipeId}'. No technology was changed.";
                return false;
            }

            if (!recipes.Contains(recipeId, StringComparer.Ordinal))
                recipes.Add(recipeId);
        }

        set.WeeklyTechnologies.Add(new WeeklyTechnologyEntry
        {
            TechnologyId = technologyId,
            Branch = branch,
            Cost = cost,
            Tier = tier,
            RecipeIds = recipes,
        });

        set.WeeklyTechnologies.Sort((a, b) => string.Compare(a.TechnologyId, b.TechnologyId, StringComparison.Ordinal));

        if (!TryCommitResearchConfigChange(originalSet, set, out message))
            return false;

        message = $"Added weekly technology '{technologyId}' to branch '{branch}' in set '{set.SetId}'.";
        return true;
    }

    public bool TryUpdateTechnology(
        string setId,
        string technologyId,
        string branch,
        int cost,
        int tier,
        IReadOnlyList<string> recipeIds,
        out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);

        if (!TryNormalizeTechnologyBranch(branch, out branch, out message))
            return false;

        technologyId = technologyId.Trim();
        var entry = set.WeeklyTechnologies.FirstOrDefault(entry =>
            string.Equals(entry.TechnologyId, technologyId, StringComparison.Ordinal));
        if (entry == null)
        {
            message = $"Weekly set '{set.SetId}' has no technology '{technologyId}'.";
            return false;
        }

        if (cost < 0)
        {
            message = "Technology cost may not be negative.";
            return false;
        }

        if (tier is < 1 or > 3)
        {
            message = "Technology tier must be 1, 2 or 3.";
            return false;
        }

        var recipeConfig = _store.LoadRecipesOrDefault(set.SetId);
        var weeklyRecipeIds = recipeConfig.Recipes.Select(recipe => recipe.Id).ToHashSet(StringComparer.Ordinal);
        var recipes = new List<string>();
        foreach (var rawRecipe in recipeIds)
        {
            var recipeId = rawRecipe.Trim();
            if (!_prototype.TryIndex<LatheRecipePrototype>(recipeId, out _) &&
                !weeklyRecipeIds.Contains(recipeId))
            {
                message = $"Unknown lathe or weekly recipe '{recipeId}'. No technology was changed.";
                return false;
            }

            if (!recipes.Contains(recipeId, StringComparer.Ordinal))
                recipes.Add(recipeId);
        }

        if (HasPurchasedWeeklyTechnology(set.SetId, technologyId))
        {
            var removedRecipes = entry.RecipeIds
                .Where(recipe => !recipes.Contains(recipe, StringComparer.Ordinal))
                .ToList();
            if (removedRecipes.Count > 0)
            {
                message = $"Weekly technology '{technologyId}' is already purchased. Refusing to remove unlocked recipes without an explicit remove --force. Removed recipes would be: {FormatList(removedRecipes)}.";
                return false;
            }
        }

        entry.Branch = branch;
        entry.Cost = cost;
        entry.Tier = tier;
        entry.RecipeIds = recipes;
        set.WeeklyTechnologies.Sort((a, b) => string.Compare(a.TechnologyId, b.TechnologyId, StringComparison.Ordinal));

        if (!TryCommitResearchConfigChange(originalSet, set, out message))
            return false;

        message = $"Updated weekly technology '{technologyId}' in set '{set.SetId}'.";
        return true;
    }

    public bool TryRemoveTechnology(string setId, string branch, string technologyId, bool force, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);

        if (!TryNormalizeTechnologyBranch(branch, out branch, out message))
            return false;

        if (!force && HasPurchasedWeeklyTechnology(set.SetId, technologyId))
        {
            message = $"Weekly technology '{technologyId}' is already purchased in the active campaign. Use '--force' to remove it and explicitly drop recipes that only come from that weekly technology.";
            return false;
        }

        var removed = set.WeeklyTechnologies.RemoveAll(entry =>
            string.Equals(entry.Branch, branch, StringComparison.Ordinal) &&
            string.Equals(entry.TechnologyId, technologyId, StringComparison.Ordinal));

        if (removed > 0 && !TryCommitResearchConfigChange(originalSet, set, out message))
            return false;

        message = removed == 0
            ? $"Weekly set '{set.SetId}' had no technology '{technologyId}' in branch '{branch}'."
            : $"Removed technology '{technologyId}' from branch '{branch}' in weekly set '{set.SetId}'.";
        return true;
    }

    public string ListTechnologies(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        if (set.WeeklyTechnologies.Count == 0)
            return $"Weekly set '{set.SetId}' has an empty research tree.";

        var lines = new List<string> { $"Weekly research tree for '{set.SetId}':" };
        foreach (var entry in set.WeeklyTechnologies
                     .OrderBy(x => x.Branch, StringComparer.Ordinal)
                     .ThenBy(x => x.TechnologyId, StringComparer.Ordinal))
        {
            lines.Add($"- {entry.Branch}: {entry.TechnologyId} cost={entry.Cost} tier={entry.Tier} recipes=[{FormatList(entry.RecipeIds)}]");
        }

        return string.Join('\n', lines);
    }

    public bool TryClearTechnologies(string setId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);
        var purchased = GetPurchasedWeeklyTechnologies(set.SetId);
        var removedPurchased = set.WeeklyTechnologies
            .Where(entry => purchased.Contains(entry.TechnologyId))
            .Select(entry => entry.TechnologyId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (removedPurchased.Count > 0)
        {
            message = $"Refusing to clear purchased weekly technologies from active set '{set.SetId}': {FormatList(removedPurchased)}.";
            return false;
        }

        var removed = set.WeeklyTechnologies.Count;
        set.WeeklyTechnologies.Clear();

        if (!TryCommitResearchConfigChange(originalSet, set, out message))
            return false;

        message = $"Cleared {removed} weekly technologies from set '{set.SetId}'.";
        return true;
    }

    public bool TryClearTechnologyBranch(string setId, string branch, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (!TryNormalizeTechnologyBranch(branch, out branch, out message))
            return false;

        var originalSet = CloneSet(set);
        var purchased = GetPurchasedWeeklyTechnologies(set.SetId);
        var removedPurchased = set.WeeklyTechnologies
            .Where(entry =>
                string.Equals(entry.Branch, branch, StringComparison.Ordinal) &&
                purchased.Contains(entry.TechnologyId))
            .Select(entry => entry.TechnologyId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (removedPurchased.Count > 0)
        {
            message = $"Refusing to clear purchased weekly technologies from branch '{branch}' in active set '{set.SetId}': {FormatList(removedPurchased)}.";
            return false;
        }

        var removed = set.WeeklyTechnologies.RemoveAll(entry => string.Equals(entry.Branch, branch, StringComparison.Ordinal));

        if (removed > 0 && !TryCommitResearchConfigChange(originalSet, set, out message))
            return false;

        message = $"Cleared {removed} weekly technologies from branch '{branch}' in set '{set.SetId}'.";
        return true;
    }

    public string ValidateTechnologies(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var errors = ValidateWeeklyTechnologies(set, _store.LoadRecipesOrDefault(set.SetId)).ToList();
        return errors.Count == 0
            ? $"Weekly research tree for '{set.SetId}' is valid."
            : $"Weekly research tree for '{set.SetId}' is invalid:\n- {string.Join("\n- ", errors)}";
    }

    public bool TryAddCargoProduct(
        string setId,
        string productId,
        string category,
        int cost,
        bool boxed,
        int amount,
        string itemPrototype,
        out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);

        productId = productId.Trim();
        if (!WeeklyModeStore.IsSafeId(productId))
        {
            message = "productId must contain only ASCII letters, digits, '-', '_' or '.'.";
            return false;
        }

        if (set.WeeklyCargoProducts.Any(entry => string.Equals(entry.ProductId, productId, StringComparison.Ordinal)))
        {
            message = $"Cargo product '{productId}' already exists in weekly set '{set.SetId}'.";
            return false;
        }

        category = category.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(category))
        {
            message = "Cargo category may not be empty.";
            return false;
        }

        if (cost < 0)
        {
            message = "Cargo product cost may not be negative.";
            return false;
        }

        if (amount < 1)
        {
            message = "Cargo product amount must be at least 1.";
            return false;
        }

        itemPrototype = itemPrototype.Trim();
        if (!_prototype.TryIndex<EntityPrototype>(itemPrototype, out _))
        {
            message = $"Unknown item entity prototype '{itemPrototype}'.";
            return false;
        }

        if (boxed && amount > WeeklyCargoBoxedCapacity)
        {
            message = $"Boxed weekly cargo products may contain at most {WeeklyCargoBoxedCapacity} entities.";
            return false;
        }

        set.WeeklyCargoProducts.Add(new WeeklyCargoProductEntry
        {
            ProductId = productId,
            Category = category,
            Cost = cost,
            Boxed = boxed,
            Amount = amount,
            ItemPrototype = itemPrototype,
        });

        set.WeeklyCargoProducts.Sort((a, b) => string.Compare(a.ProductId, b.ProductId, StringComparison.Ordinal));

        if (!TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Cargo, out message))
            return false;

        message = $"Added weekly cargo product '{productId}' to category '{category}' in set '{set.SetId}'.";
        return true;
    }

    public bool TryUpdateCargoProduct(
        string setId,
        string productId,
        string category,
        int cost,
        bool boxed,
        int amount,
        string itemPrototype,
        out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);

        productId = productId.Trim();
        var entry = set.WeeklyCargoProducts.FirstOrDefault(entry =>
            string.Equals(entry.ProductId, productId, StringComparison.Ordinal));
        if (entry == null)
        {
            message = $"Weekly set '{set.SetId}' has no cargo product '{productId}'.";
            return false;
        }

        category = category.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(category))
        {
            message = "Cargo category may not be empty.";
            return false;
        }

        if (cost < 0)
        {
            message = "Cargo product cost may not be negative.";
            return false;
        }

        if (amount < 1)
        {
            message = "Cargo product amount must be at least 1.";
            return false;
        }

        itemPrototype = itemPrototype.Trim();
        if (!_prototype.TryIndex<EntityPrototype>(itemPrototype, out _))
        {
            message = $"Unknown item entity prototype '{itemPrototype}'.";
            return false;
        }

        if (boxed && amount > WeeklyCargoBoxedCapacity)
        {
            message = $"Boxed weekly cargo products may contain at most {WeeklyCargoBoxedCapacity} entities.";
            return false;
        }

        entry.Category = category;
        entry.Cost = cost;
        entry.Boxed = boxed;
        entry.Amount = amount;
        entry.ItemPrototype = itemPrototype;
        set.WeeklyCargoProducts.Sort((a, b) => string.Compare(a.ProductId, b.ProductId, StringComparison.Ordinal));

        if (!TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Cargo, out message))
            return false;

        message = $"Updated weekly cargo product '{productId}' in set '{set.SetId}'.";
        return true;
    }

    public bool TryRemoveCargoProduct(string setId, string productId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);

        var removed = set.WeeklyCargoProducts.RemoveAll(entry => string.Equals(entry.ProductId, productId, StringComparison.Ordinal));

        if (removed > 0 && !TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Cargo, out message))
            return false;

        message = removed == 0
            ? $"Weekly set '{set.SetId}' had no cargo product '{productId}'."
            : $"Removed weekly cargo product '{productId}' from set '{set.SetId}'.";
        return true;
    }

    public string ListCargoProducts(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        if (set.WeeklyCargoProducts.Count == 0)
            return $"Weekly set '{set.SetId}' has an empty cargo catalog.";

        var lines = new List<string> { $"Weekly cargo catalog for '{set.SetId}':" };
        foreach (var entry in set.WeeklyCargoProducts
                     .OrderBy(x => x.Category, StringComparer.Ordinal)
                     .ThenBy(x => x.ProductId, StringComparer.Ordinal))
        {
            lines.Add($"- {entry.Category}: {entry.ProductId} cost={entry.Cost} boxed={entry.Boxed} amount={entry.Amount} item={entry.ItemPrototype}");
        }

        return string.Join('\n', lines);
    }

    public bool TryClearCargoProducts(string setId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);
        var removed = set.WeeklyCargoProducts.Count;
        set.WeeklyCargoProducts.Clear();

        if (!TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Cargo, out message))
            return false;

        message = $"Cleared {removed} weekly cargo products from set '{set.SetId}'.";
        return true;
    }

    public bool TryClearCargoCategory(string setId, string category, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);
        category = category.Trim().Trim('"');
        var removed = set.WeeklyCargoProducts.RemoveAll(entry => string.Equals(entry.Category, category, StringComparison.Ordinal));

        if (removed > 0 && !TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Cargo, out message))
            return false;

        message = $"Cleared {removed} weekly cargo products from category '{category}' in set '{set.SetId}'.";
        return true;
    }

    public string ValidateCargoProducts(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var errors = ValidateWeeklyCargoProducts(set).ToList();
        return errors.Count == 0
            ? $"Weekly cargo catalog for '{set.SetId}' is valid."
            : $"Weekly cargo catalog for '{set.SetId}' is invalid:\n- {string.Join("\n- ", errors)}";
    }

    public bool TryAddRecipe(
        string setId,
        string recipeId,
        string resultPrototype,
        int resultAmount,
        double productionTimeSeconds,
        string latheTargets,
        IReadOnlyList<string> materialSpecs,
        out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        var originalRecipes = CloneRecipes(recipes);

        recipeId = recipeId.Trim();
        if (recipes.Recipes.Any(recipe => string.Equals(recipe.Id, recipeId, StringComparison.Ordinal)))
        {
            message = $"Weekly recipe '{recipeId}' already exists in set '{set.SetId}'.";
            return false;
        }

        if (!TryBuildWeeklyRecipeDefinition(recipeId, resultPrototype, resultAmount, productionTimeSeconds, latheTargets, materialSpecs, out var recipe, out message))
            return false;

        recipes.Recipes.Add(recipe);
        SortRecipes(recipes);
        SyncRecipeTechnologyLinks(set, recipes);

        if (!TryCommitRecipesChange(set, originalRecipes, recipes, WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = $"Added weekly recipe '{recipe.Id}' to set '{set.SetId}'.";
        return true;
    }

    public bool TryUpdateRecipe(string setId, string recipeId, string field, IReadOnlyList<string> args, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        var originalRecipes = CloneRecipes(recipes);
        var recipe = recipes.Recipes.FirstOrDefault(recipe => string.Equals(recipe.Id, recipeId, StringComparison.Ordinal));
        if (recipe == null)
        {
            message = $"Weekly set '{set.SetId}' has no recipe '{recipeId}'.";
            return false;
        }

        if (IsWeeklyRecipeInUse(recipe.Id))
        {
            message = $"Weekly recipe '{recipe.Id}' is currently queued or producing. Update refused to preserve material refunds and output consistency.";
            return false;
        }

        switch (field.Trim().ToLowerInvariant())
        {
            case "result":
                if (args.Count != 2 ||
                    !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
                {
                    message = "Usage: wm.recipe.update <setId> <recipeId> result <resultPrototype> <amount>";
                    return false;
                }

                recipe.ResultPrototype = args[0].Trim();
                recipe.ResultAmount = amount;
                break;
            case "time":
                if (args.Count != 1 ||
                    !double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    message = "Usage: wm.recipe.update <setId> <recipeId> time <seconds>";
                    return false;
                }

                recipe.ProductionTimeSeconds = seconds;
                break;
            case "targets":
                if (args.Count != 1 || !TryNormalizeLatheTargets(args[0], out var targets, out message))
                    return false;

                recipe.LatheTargets = targets;
                break;
            case "materials":
                if (args.Count < 1 || !TryParseRecipeMaterials(args, out var materials, out message))
                    return false;

                recipe.Materials = materials;
                break;
            default:
                message = "Recipe update field must be one of: result, time, targets, materials.";
                return false;
        }

        SortRecipes(recipes);
        SyncRecipeTechnologyLinks(set, recipes);

        if (!TryCommitRecipesChange(set, originalRecipes, recipes, WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = $"Updated weekly recipe '{recipe.Id}' in set '{set.SetId}'.";
        return true;
    }

    public bool TryRemoveRecipe(string setId, string recipeId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        var originalRecipes = CloneRecipes(recipes);
        var recipe = recipes.Recipes.FirstOrDefault(recipe => string.Equals(recipe.Id, recipeId, StringComparison.Ordinal));
        if (recipe == null)
        {
            message = $"Weekly set '{set.SetId}' had no recipe '{recipeId}'.";
            return false;
        }

        if (IsWeeklyRecipeInUse(recipe.Id))
        {
            message = $"Weekly recipe '{recipe.Id}' is currently queued or producing. Remove refused.";
            return false;
        }

        var linkedTechnologies = set.WeeklyTechnologies
            .Where(technology => technology.RecipeIds.Contains(recipe.Id, StringComparer.Ordinal))
            .Select(technology => technology.TechnologyId)
            .ToList();
        if (linkedTechnologies.Count > 0)
        {
            message = $"Weekly recipe '{recipe.Id}' is linked to technologies and was not removed. Unlink it first: {FormatList(linkedTechnologies)}.";
            return false;
        }

        recipes.Recipes.Remove(recipe);
        SortRecipes(recipes);
        SyncRecipeTechnologyLinks(set, recipes);

        if (!TryCommitRecipesChange(set, originalRecipes, recipes, WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = $"Removed weekly recipe '{recipeId}' from set '{set.SetId}'.";
        return true;
    }

    public bool TryClearRecipes(string setId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        var inUse = recipes.Recipes
            .Where(recipe => IsWeeklyRecipeInUse(recipe.Id))
            .Select(recipe => recipe.Id)
            .ToList();
        if (inUse.Count > 0)
        {
            message = $"Refusing to clear weekly recipes currently queued or producing: {FormatList(inUse)}.";
            return false;
        }

        var originalRecipes = CloneRecipes(recipes);
        var removed = recipes.Recipes.Count;
        var linkedRecipes = recipes.Recipes
            .Where(recipe => set.WeeklyTechnologies.Any(technology => technology.RecipeIds.Contains(recipe.Id, StringComparer.Ordinal)))
            .Select(recipe => recipe.Id)
            .ToList();
        if (linkedRecipes.Count > 0)
        {
            message = $"Refusing to clear weekly recipes still linked to technologies: {FormatList(linkedRecipes)}.";
            return false;
        }

        recipes.Recipes.Clear();

        if (!TryCommitRecipesChange(set, originalRecipes, recipes, WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = $"Cleared {removed} weekly recipes from set '{set.SetId}'.";
        return true;
    }

    public bool TryAddRecipeTarget(string setId, string recipeId, string target, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        var originalRecipes = CloneRecipes(recipes);
        var recipe = recipes.Recipes.FirstOrDefault(recipe => string.Equals(recipe.Id, recipeId, StringComparison.Ordinal));
        if (recipe == null)
        {
            message = $"Weekly set '{set.SetId}' has no recipe '{recipeId}'.";
            return false;
        }

        if (IsWeeklyRecipeInUse(recipe.Id))
        {
            message = $"Weekly recipe '{recipe.Id}' is currently queued or producing. Target update refused.";
            return false;
        }

        if (!TryNormalizeLatheTarget(target, out var normalizedTarget, out message))
            return false;

        if (!recipe.LatheTargets.Contains(normalizedTarget, StringComparer.Ordinal))
            recipe.LatheTargets.Add(normalizedTarget);
        recipe.LatheTargets.Sort(StringComparer.Ordinal);
        SyncRecipeTechnologyLinks(set, recipes);

        if (!TryCommitRecipesChange(set, originalRecipes, recipes, WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = $"Added target '{normalizedTarget}' to weekly recipe '{recipe.Id}'.";
        return true;
    }

    public bool TryRemoveRecipeTarget(string setId, string recipeId, string target, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        var originalRecipes = CloneRecipes(recipes);
        var recipe = recipes.Recipes.FirstOrDefault(recipe => string.Equals(recipe.Id, recipeId, StringComparison.Ordinal));
        if (recipe == null)
        {
            message = $"Weekly set '{set.SetId}' has no recipe '{recipeId}'.";
            return false;
        }

        if (IsWeeklyRecipeInUse(recipe.Id))
        {
            message = $"Weekly recipe '{recipe.Id}' is currently queued or producing. Target update refused.";
            return false;
        }

        if (!TryNormalizeLatheTarget(target, out var normalizedTarget, out message))
            return false;

        recipe.LatheTargets.RemoveAll(existing => string.Equals(existing, normalizedTarget, StringComparison.Ordinal));
        SyncRecipeTechnologyLinks(set, recipes);

        if (!TryCommitRecipesChange(set, originalRecipes, recipes, WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = $"Removed target '{normalizedTarget}' from weekly recipe '{recipe.Id}'.";
        return true;
    }

    public bool TryLinkRecipe(string setId, string recipeId, string technologyId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var technology = set.WeeklyTechnologies.FirstOrDefault(entry => string.Equals(entry.TechnologyId, technologyId, StringComparison.Ordinal));
        if (technology == null)
        {
            message = $"Weekly set '{set.SetId}' has no technology '{technologyId}'.";
            return false;
        }

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        if (!recipes.Recipes.Any(recipe => string.Equals(recipe.Id, recipeId, StringComparison.Ordinal)))
        {
            message = $"Weekly set '{set.SetId}' has no recipe '{recipeId}'.";
            return false;
        }

        if (technology.RecipeIds.Contains(recipeId, StringComparer.Ordinal))
        {
            message = $"Weekly recipe '{recipeId}' is already linked to technology '{technologyId}'.";
            return false;
        }

        var originalSet = CloneSet(set);
        var originalRecipes = CloneRecipes(recipes);
        technology.RecipeIds.Add(recipeId);
        technology.RecipeIds.Sort(StringComparer.Ordinal);
        SyncRecipeTechnologyLinks(set, recipes);

        if (!TryCommitConfigAndRecipesChange(originalSet, set, originalRecipes, recipes, WeeklyLiveConfigChange.Research | WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = $"Linked weekly recipe '{recipeId}' to technology '{technologyId}'.";
        return true;
    }

    public bool TryUnlinkRecipe(string setId, string recipeId, string technologyId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (IsWeeklyRecipeInUse(recipeId))
        {
            message = $"Weekly recipe '{recipeId}' is currently queued or producing. Unlink refused.";
            return false;
        }

        var technology = set.WeeklyTechnologies.FirstOrDefault(entry => string.Equals(entry.TechnologyId, technologyId, StringComparison.Ordinal));
        if (technology == null)
        {
            message = $"Weekly set '{set.SetId}' has no technology '{technologyId}'.";
            return false;
        }

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        var originalSet = CloneSet(set);
        var originalRecipes = CloneRecipes(recipes);
        var removed = technology.RecipeIds.RemoveAll(id => string.Equals(id, recipeId, StringComparison.Ordinal));
        SyncRecipeTechnologyLinks(set, recipes);

        if (removed > 0 && !TryCommitConfigAndRecipesChange(originalSet, set, originalRecipes, recipes, WeeklyLiveConfigChange.Research | WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = removed == 0
            ? $"Weekly recipe '{recipeId}' was not linked to technology '{technologyId}'."
            : $"Unlinked weekly recipe '{recipeId}' from technology '{technologyId}'.";
        return true;
    }

    public string ListRecipes(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        SyncRecipeTechnologyLinks(set, recipes);
        if (recipes.Recipes.Count == 0)
            return $"Weekly set '{set.SetId}' has no campaign recipes.";

        var lines = new List<string> { $"Weekly recipes for '{set.SetId}':" };
        foreach (var recipe in recipes.Recipes.OrderBy(recipe => recipe.Id, StringComparer.Ordinal))
        {
            lines.Add($"- {recipe.Id}: result={recipe.ResultPrototype}x{recipe.ResultAmount} time={recipe.ProductionTimeSeconds.ToString(CultureInfo.InvariantCulture)}s targets=[{FormatList(recipe.LatheTargets)}] materials=[{FormatDictionary(recipe.Materials)}] technologies=[{FormatList(recipe.TechnologyIds)}]");
        }

        return string.Join('\n', lines);
    }

    public string ShowRecipe(string setId, string recipeId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        SyncRecipeTechnologyLinks(set, recipes);
        var recipe = recipes.Recipes.FirstOrDefault(recipe => string.Equals(recipe.Id, recipeId, StringComparison.Ordinal));
        if (recipe == null)
            return $"Weekly set '{set.SetId}' has no recipe '{recipeId}'.";

        return string.Join('\n', new[]
        {
            $"Weekly recipe '{recipe.Id}' in set '{set.SetId}':",
            $"- resultPrototype: {recipe.ResultPrototype}",
            $"- resultAmount: {recipe.ResultAmount}",
            $"- productionTimeSeconds: {recipe.ProductionTimeSeconds.ToString(CultureInfo.InvariantCulture)}",
            $"- latheTargets: {FormatList(recipe.LatheTargets)}",
            $"- materials: {FormatDictionary(recipe.Materials)}",
            $"- technologyIds: {FormatList(recipe.TechnologyIds)}",
        });
    }

    public string ValidateRecipes(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        SyncRecipeTechnologyLinks(set, recipes);
        var errors = ValidateWeeklyRecipes(set, recipes).ToList();
        errors.AddRange(ValidateWeeklyTechnologies(set, recipes));
        return errors.Count == 0
            ? $"Weekly recipes for '{set.SetId}' are valid."
            : $"Weekly recipes for '{set.SetId}' are invalid:\n- {string.Join("\n- ", errors)}";
    }

    public string ShowConfig(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var lines = new List<string>
        {
            $"Weekly config for '{set.SetId}':",
            $"- schemaVersion: {set.SchemaVersion}",
            $"- displayName: {set.DisplayName}",
            $"- baseMapPrototype: {set.BaseMapPrototype}",
            $"- baseMapPath: {set.BaseMapPath}",
            $"- currentSnapshot: {set.CurrentSnapshot ?? "<base>"}",
            $"- autosave: interval={set.AutosaveMinutes}m warning={set.AutosaveWarningMinutes}m retain={set.RetainAutosaves}",
            $"- disabledRoles: {FormatList(set.DefaultDisabledJobs)}",
            $"- roleAliases: {FormatDictionary(set.DefaultRoleAliases)}",
            $"- roleLimits: {FormatDictionary(set.DefaultRoleLimits)}",
            $"- forcedRoleAssignments: {set.ForcedRoleAssignments.Count} entries",
            $"- persistAutonomousMobs: {set.PersistAutonomousMobs}",
            $"- persistPlayerControlledBorgs: {set.PersistPlayerControlledBorgs}",
            $"- excludedMobPrototypes: {FormatList(set.ExcludedMobPrototypes)}",
            $"- access: minPlaytimeHours={set.MinPlaytimeHours} discord={(string.IsNullOrWhiteSpace(set.DiscordChannel) ? "<none>" : set.DiscordChannel)}",
            $"- randomGameRulesEnabled: {set.RandomGameRulesEnabled}",
            $"- researchTree: {set.WeeklyTechnologies.Count} entries",
            $"- cargoCatalog: {set.WeeklyCargoProducts.Count} entries",
            $"- snapshots: {set.Snapshots.Count}",
            $"- active: {IsActiveSet(set.SetId)}",
        };

        return string.Join('\n', lines);
    }

    public string ValidateConfig(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        if (TryValidateSetConfig(set, out var errors))
            return $"Weekly set '{set.SetId}' config is valid.";

        return $"Weekly set '{set.SetId}' config is invalid:\n- {string.Join("\n- ", errors)}";
    }

    public string ExportConfig(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        return _store.ExportSetJson(set);
    }

    public bool TryGetRoleAlias(string jobId, [NotNullWhen(true)] out string? alias)
    {
        alias = null;
        if (!_state.IsActive || _state.ActiveSetId == null)
            return false;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return false;

        return set.DefaultRoleAliases.TryGetValue(jobId, out alias) && !string.IsNullOrWhiteSpace(alias);
    }

    public string GetRoleDisplayName(string jobId, string fallback)
    {
        return TryGetRoleAlias(jobId, out var alias) ? alias : fallback;
    }

    public string GetJobDisplayName(ProtoId<JobPrototype> jobId)
    {
        if (_prototype.TryIndex<JobPrototype>(jobId, out var job))
            return GetRoleDisplayName(job.ID, job.LocalizedName);

        return jobId.Id;
    }

    public Dictionary<ProtoId<JobPrototype>, string> GetActiveRoleAliases()
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return new();

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return new();

        var aliases = new Dictionary<ProtoId<JobPrototype>, string>();
        foreach (var (jobId, alias) in set.DefaultRoleAliases)
        {
            if (_prototype.HasIndex<JobPrototype>(jobId))
                aliases[new ProtoId<JobPrototype>(jobId)] = alias;
        }

        return aliases;
    }

    public bool ShouldSuppressAutomaticGameRules()
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return false;

        return _store.TryLoadSet(_state.ActiveSetId, out var set) && !set.RandomGameRulesEnabled;
    }

    public bool TryGetActiveWeeklyCargoProducts(out List<WeeklyCargoProductData> products)
    {
        products = new List<WeeklyCargoProductData>();
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return false;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return false;

        foreach (var entry in set.WeeklyCargoProducts)
        {
            if (TryBuildWeeklyCargoProductData(entry, out var product))
                products.Add(product);
        }

        products.Sort((a, b) => string.Compare(a.ProductId, b.ProductId, StringComparison.Ordinal));

        return true;
    }

    public bool TryGetActiveWeeklyCargoProduct(string productId, out WeeklyCargoProductData product)
    {
        product = default;
        if (!TryGetActiveWeeklyCargoProducts(out var products))
            return false;

        foreach (var candidate in products)
        {
            if (!string.Equals(candidate.ProductId, productId, StringComparison.Ordinal))
                continue;

            product = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetActiveWeeklyCargoProductIds(out HashSet<string> productIds)
    {
        productIds = new HashSet<string>(StringComparer.Ordinal);
        if (!TryGetActiveWeeklyCargoProducts(out var products))
            return false;

        foreach (var entry in products)
            productIds.Add(entry.ProductId);

        return true;
    }

    public List<WeeklyLatheRecipeData> GetAvailableWeeklyLatheRecipes(EntityUid latheUid, LatheComponent lathe, bool getUnavailable = false)
    {
        var recipes = new List<WeeklyLatheRecipeData>();
        if (!TryGetActiveWeeklyRecipeDefinitions(out var definitions))
            return recipes;

        foreach (var definition in definitions)
        {
            if (!IsWeeklyRecipeTargetMatch(latheUid, lathe, definition))
                continue;

            if (!getUnavailable && !IsWeeklyRecipeUnlockedForLathe(latheUid, definition))
                continue;

            if (TryBuildWeeklyLatheRecipeData(definition, out var data))
                recipes.Add(data);
        }

        recipes.Sort((a, b) => string.Compare(a.RecipeId, b.RecipeId, StringComparison.Ordinal));
        return recipes;
    }

    public bool TryGetAvailableWeeklyLatheRecipe(EntityUid latheUid, LatheComponent lathe, string recipeId, out WeeklyLatheRecipeData recipe)
    {
        recipe = default;
        foreach (var candidate in GetAvailableWeeklyLatheRecipes(latheUid, lathe))
        {
            if (!string.Equals(candidate.RecipeId, recipeId, StringComparison.Ordinal))
                continue;

            recipe = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetActiveWeeklyLatheRecipe(string recipeId, out WeeklyLatheRecipeData recipe)
    {
        recipe = default;
        if (!TryGetActiveWeeklyRecipeDefinitions(out var definitions))
            return false;

        foreach (var definition in definitions)
        {
            if (!string.Equals(definition.Id, recipeId, StringComparison.Ordinal))
                continue;

            return TryBuildWeeklyLatheRecipeData(definition, out recipe);
        }

        return false;
    }

    private bool TryBuildWeeklyCargoProductData(WeeklyCargoProductEntry entry, out WeeklyCargoProductData product)
    {
        product = default;
        if (!_prototype.TryIndex<EntityPrototype>(entry.ItemPrototype, out var itemPrototype))
            return false;

        product = new WeeklyCargoProductData
        {
            ProductId = entry.ProductId,
            Name = itemPrototype.Name,
            Description = itemPrototype.Description,
            Category = entry.Category,
            Cost = entry.Cost,
            Boxed = entry.Boxed,
            Amount = entry.Amount,
            ItemPrototype = entry.ItemPrototype,
            Icon = new SpriteSpecifier.EntityPrototype(entry.ItemPrototype),
        };

        return true;
    }

    private bool TryGetActiveWeeklyRecipeDefinitions(out List<WeeklyRecipeDefinition> recipes)
    {
        recipes = new List<WeeklyRecipeDefinition>();
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return false;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return false;

        var config = _store.LoadRecipesOrDefault(set.SetId);
        SyncRecipeTechnologyLinks(set, config);
        recipes = config.Recipes;
        return true;
    }

    private bool IsWeeklyRecipeUnlockedForLathe(EntityUid latheUid, WeeklyRecipeDefinition recipe)
    {
        if (!TryComp<TechnologyDatabaseComponent>(latheUid, out var database) || !database.WeeklyModeOnly)
            return false;

        foreach (var technologyId in recipe.TechnologyIds)
        {
            foreach (var unlockedTechnologyId in database.WeeklyUnlockedTechnologies)
            {
                if (string.Equals(unlockedTechnologyId, technologyId, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private bool IsWeeklyRecipeTargetMatch(EntityUid latheUid, LatheComponent lathe, WeeklyRecipeDefinition recipe)
    {
        if (recipe.LatheTargets.Count == 0)
            return false;

        var prototypeId = MetaData(latheUid).EntityPrototype?.ID;
        foreach (var target in recipe.LatheTargets)
        {
            if (IsWeeklyRecipeTargetMatch(target, prototypeId, lathe))
                return true;
        }

        return false;
    }

    private bool IsWeeklyRecipeTargetMatch(string target, string? entityPrototypeId, LatheComponent lathe)
    {
        if (WeeklyRecipeTargetAliases.TryGetValue(target, out var alias))
        {
            if (alias.All)
                return true;

            if (entityPrototypeId != null &&
                alias.Entities.Any(entity => string.Equals(entity, entityPrototypeId, StringComparison.Ordinal)))
            {
                return true;
            }

            return alias.Packs.Any(pack => LatheHasPack(lathe, pack));
        }

        if (target.StartsWith("entity:", StringComparison.OrdinalIgnoreCase))
        {
            var id = target["entity:".Length..];
            return entityPrototypeId != null && string.Equals(id, entityPrototypeId, StringComparison.Ordinal);
        }

        if (target.StartsWith("pack:", StringComparison.OrdinalIgnoreCase))
        {
            var id = target["pack:".Length..];
            return LatheHasPack(lathe, id);
        }

        return false;
    }

    private static bool LatheHasPack(LatheComponent lathe, string packId)
    {
        return lathe.StaticPacks.Any(pack => string.Equals(pack.Id, packId, StringComparison.Ordinal)) ||
               lathe.DynamicPacks.Any(pack => string.Equals(pack.Id, packId, StringComparison.Ordinal));
    }

    private bool TryBuildWeeklyLatheRecipeData(WeeklyRecipeDefinition definition, out WeeklyLatheRecipeData recipe)
    {
        recipe = default;
        if (!_prototype.TryIndex<EntityPrototype>(definition.ResultPrototype, out var resultPrototype))
            return false;

        recipe = new WeeklyLatheRecipeData
        {
            RecipeId = definition.Id,
            Name = resultPrototype.Name,
            Description = resultPrototype.Description,
            ResultPrototype = definition.ResultPrototype,
            ResultAmount = definition.ResultAmount,
            ProductionTimeSeconds = definition.ProductionTimeSeconds,
            ApplyMaterialDiscount = definition.ApplyMaterialDiscount,
            Materials = definition.Materials
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new WeeklyLatheRecipeMaterialData
                {
                    MaterialId = pair.Key,
                    Amount = pair.Value,
                })
                .ToList(),
            Icon = new SpriteSpecifier.EntityPrototype(definition.ResultPrototype),
        };

        return true;
    }

    public bool IsWeeklyAccessAllowed(ICommonSession session, bool notify)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return true;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return true;

        return IsWeeklyAccessAllowed(session, set, notify);
    }

    private bool IsWeeklyAccessAllowed(ICommonSession session, WeeklyModeSet set, bool notify)
    {
        if (set.MinPlaytimeHours <= 0)
            return true;

        var required = TimeSpan.FromHours(set.MinPlaytimeHours);
        var current = GetOverallPlaytimeOrZero(session);
        if (current >= required)
            return true;

        if (notify)
            SendAccessDeniedNotice(session, set, false);

        return false;
    }

    private TimeSpan GetOverallPlaytimeOrZero(ICommonSession session)
    {
        return _playTime.TryGetTrackerTimes(session, out var times) &&
               times.TryGetValue(PlayTimeTrackingShared.TrackerOverall, out var overall)
            ? overall
            : TimeSpan.Zero;
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return;

        if (TryGetForcedAssignment(set, ev.PlayerSession.UserId, out var forcedAssignment))
            NotifyForcedRole(ev.PlayerSession, set, forcedAssignment, false);

        if (set.MinPlaytimeHours <= 0 || IsWeeklyAccessAllowed(ev.PlayerSession, set, false))
            return;

        if (_accessLobbyNoticeSent.Add(ev.PlayerSession.UserId))
            SendAccessDeniedNotice(ev.PlayerSession, set, true);
    }

    private void OnStationJobsGetCandidates(ref StationJobsGetCandidatesEvent ev)
    {
        if (!_playerManager.TryGetSessionById(ev.Player, out var session))
            return;

        if (IsWeeklyAccessAllowed(session, false))
            return;

        if (TryGetActiveForcedAssignment(ev.Player, out _, out var assignment) &&
            assignment.BypassPlaytime)
        {
            ev.Jobs.Clear();
            ev.Jobs.Add(new ProtoId<JobPrototype>(assignment.JobId));
            return;
        }

        if (!IsWeeklyAccessAllowed(session, true))
            ev.Jobs.Clear();
    }

    private void OnIsRoleAllowed(ref IsRoleAllowedEvent ev)
    {
        if (ev.Jobs is { Count: > 0 })
        {
            foreach (var job in ev.Jobs)
            {
                if (!CanLateJoinJob(ev.Player, EntityUid.Invalid, job, out _))
                {
                    ev.Cancelled = true;
                    return;
                }
            }
        }

        if (ev.Jobs is { Count: 1 } &&
            IsWeeklyAccessAllowedForJob(ev.Player, ev.Jobs[0].Id, true))
        {
            return;
        }

        if (IsWeeklyAccessAllowed(ev.Player, true))
            return;

        ev.Cancelled = true;
    }

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent ev)
    {
        if (IsWeeklyAccessAllowed(ev.Player, false))
        {
            foreach (var job in _prototype.EnumeratePrototypes<JobPrototype>())
            {
                var jobId = new ProtoId<JobPrototype>(job.ID);
                if (IsJobFullyReservedForOthers(jobId) &&
                    (!TryGetActiveForcedAssignment(ev.Player.UserId, out _, out var assignment) ||
                     !string.Equals(assignment.JobId, job.ID, StringComparison.Ordinal)))
                {
                    ev.Jobs.Add(jobId);
                }
            }

            return;
        }

        if (TryGetActiveForcedAssignment(ev.Player.UserId, out _, out var forcedAssignment) &&
            forcedAssignment.BypassPlaytime)
        {
            foreach (var job in _prototype.EnumeratePrototypes<JobPrototype>())
            {
                if (!string.Equals(job.ID, forcedAssignment.JobId, StringComparison.Ordinal))
                    ev.Jobs.Add(job.ID);
            }

            return;
        }

        foreach (var job in _prototype.EnumeratePrototypes<JobPrototype>())
            ev.Jobs.Add(job.ID);

        SendAccessDeniedNotice(ev.Player, LoadActiveSetOrNull(), false);
    }

    private WeeklyModeSet? LoadActiveSetOrNull()
    {
        return _state.ActiveSetId != null && _store.TryLoadSet(_state.ActiveSetId, out var set)
            ? set
            : null;
    }

    private void OnLoadingMaps(LoadingMapsEvent ev)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
        {
            _sawmill.Error($"Weekly map selection failed: set '{_state.ActiveSetId}' is missing.");
            throw new InvalidOperationException($"Weekly map selection failed: set '{_state.ActiveSetId}' is missing.");
        }

        ClearActiveWeeklyMapTracking();
        var previousMap = GetCurrentOrConfiguredMapName();
        var snapshotId = _state.PendingSnapshotId ?? set.CurrentSnapshot;
        if (snapshotId != null)
        {
            if (!TryLoadCompatibleSnapshot(set, snapshotId, out _, out var validationMessage))
                throw new InvalidOperationException($"Weekly snapshot selection failed: {validationMessage}");

            var stationPath = _store.SnapshotStationPath(set.SetId, snapshotId);
            _gameMapManager.SelectPersistentMap(set.BaseMapPrototype, stationPath);
            _state.ActiveSnapshotId = snapshotId;
            _state.PendingSnapshotId = null;
            _store.SaveState(_state);
            _sawmill.Info(
                "Weekly snapshot start:\n" +
                $"set={set.SetId}\n" +
                $"snapshot={snapshotId}\n" +
                $"stationPath={stationPath}\n" +
                $"previousMap={previousMap}");
        }
        else
        {
            if (TryGetConfiguredBaseMapPath(set, out var baseMapPath))
                _gameMapManager.SelectMapPath(set.BaseMapPrototype, baseMapPath);
            else
                _gameMapManager.SelectMap(set.BaseMapPrototype);

            _state.ActiveSnapshotId = null;
            _state.PendingSnapshotId = null;
            _store.SaveState(_state);
            _sawmill.Info(
                "Weekly first start:\n" +
                $"set={set.SetId}\n" +
                $"baseMapPrototype={set.BaseMapPrototype}\n" +
                $"baseMapPath={set.BaseMapPath}\n" +
                "source=BaseMap\n" +
                $"previousMap={previousMap}");
        }

        var selected = _gameMapManager.GetSelectedMap();
        if (selected == null)
            throw new InvalidOperationException($"Weekly map selection failed: base map '{set.BaseMapPrototype}' could not be selected.");

        if (!string.Equals(selected.ID, set.BaseMapPrototype, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Weekly map selection failed: selected map does not match the map assigned to this Weekly set.\n" +
                $"Expected: {set.BaseMapPrototype}\n" +
                $"Actual: {selected.ID}");
        }

        ev.Maps.Clear();
        ev.Maps.Add(selected);
    }

    private void OnPreGameMapLoad(PreGameMapLoad ev)
    {
        if (!_enabled ||
            !_state.IsActive ||
            _state.ActiveSetId == null ||
            (_state.PendingSnapshotId ?? _state.ActiveSnapshotId) == null)
        {
            return;
        }

        ev.Options = ev.Options with { StoreYamlUids = true };
    }

    private void OnPostGameMapLoad(PostGameMapLoad ev)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return;

        var (mapEntity, gridUids) = TrackActiveWeeklyMap(set, ev);
        ApplyWeeklyResearchOverlay(set);

        if (_state.ActiveSnapshotId == null)
            return;

        SuppressSnapshotMapInitOnlyComponents(ev.Map, mapEntity, gridUids);
        SuppressSnapshotStartingItems(ev.Map, mapEntity, gridUids);

        if (!_store.TryLoadContainerPatch(_state.ActiveSetId, _state.ActiveSnapshotId, out var patch))
        {
            _sawmill.Warning($"Weekly snapshot '{_state.ActiveSnapshotId}' has no readable container patch.");
            return;
        }

        ApplyContainerPatch(ev.Map, mapEntity, gridUids, patch, _state.ActiveSetId, _state.ActiveSnapshotId);
        DeactivateLoadedSnapshotBorgs(mapEntity, gridUids);
    }

    private void OnStationInitialized(StationInitializedEvent ev)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return;

        ApplyRoleOverridesToStation(ev.Station, set);
        ApplyWeeklyResearchOverlay(set);
    }

    private void OnTechnologyDatabaseStartup(EntityUid uid, TechnologyDatabaseComponent component, ComponentStartup args)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return;

        _research.SetWeeklyModeOverlay(uid, true, BuildWeeklyResearchData(set), true, component);
        RaiseLocalEvent(new WeeklyRecipesChangedEvent());
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
        {
            _nextAutosaveAt = null;
            _autosaveWarningIssued = false;
            return;
        }

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
        {
            _nextAutosaveAt = null;
            _autosaveWarningIssued = false;
            return;
        }

        _nextAutosaveAt = _timing.CurTime + TimeSpan.FromMinutes(Math.Max(1, set.AutosaveMinutes));
        _autosaveWarningIssued = false;
    }

    private (EntityUid MapEntity, List<EntityUid> GridUids) TrackActiveWeeklyMap(WeeklyModeSet set, PostGameMapLoad ev)
    {
        if (!string.Equals(ev.GameMap.ID, set.BaseMapPrototype, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Weekly map load failed: loaded map does not match the map assigned to this Weekly set.\n" +
                $"Expected: {set.BaseMapPrototype}\n" +
                $"Actual: {ev.GameMap.ID}");
        }

        var mapEntity = _map.GetMapOrInvalid(ev.Map);
        if (!mapEntity.IsValid() ||
            !_metaQuery.TryGetComponent(mapEntity, out var meta) ||
            meta.EntityLifeStage >= EntityLifeStage.Terminating)
        {
            throw new InvalidOperationException($"Weekly map load failed: loaded map {ev.Map} has no live map entity.");
        }

        var mapName = string.IsNullOrWhiteSpace(meta.EntityName)
            ? ev.GameMap.MapName
            : meta.EntityName;

        if (IsDevMapMismatch(set.BaseMapPrototype, mapName))
        {
            throw new InvalidOperationException(
                "Weekly map load failed: active map does not match the map assigned to this Weekly set.\n" +
                $"Expected: {set.BaseMapPrototype}\n" +
                $"Actual: {mapName}");
        }

        var gridUids = CollectGridUids(ev.Map);
        foreach (var uid in ev.Grids)
        {
            if (uid.IsValid() && !gridUids.Contains(uid))
                gridUids.Add(uid);
        }

        _activeWeeklyMapId = ev.Map;
        _activeWeeklyMapEntity = mapEntity;
        _activeWeeklyGridIds.Clear();
        _activeWeeklyGridIds.AddRange(gridUids);
        _activeWeeklyMapPrototype = set.BaseMapPrototype;
        _activeWeeklyMapName = mapName;

        _state.ActiveWeeklyMapId = (int) ev.Map;
        _state.ActiveWeeklyMapEntity = mapEntity.Id;
        _state.ActiveWeeklyGridIds = gridUids.Select(uid => uid.Id).OrderBy(id => id).ToList();
        _state.ActiveWeeklyMapPrototype = set.BaseMapPrototype;
        _state.ActiveWeeklyMapName = mapName;
        _store.SaveState(_state);

        var entityCount = CountEntitiesOnMap(ev.Map, mapEntity, gridUids);
        var prefix = _state.ActiveSnapshotId == null
            ? "Weekly first start loaded:"
            : "Weekly snapshot loaded:";

        _sawmill.Info(
            $"{prefix}\n" +
            $"newMapId={(int) ev.Map}\n" +
            $"mapEntity={mapEntity.Id}\n" +
                $"mapName={mapName}\n" +
                $"grids={string.Join(",", gridUids.Select(uid => uid.Id))}\n" +
                $"entities={entityCount}");

        return (mapEntity, gridUids);
    }

    private bool TryResolveActiveWeeklyMap(
        WeeklyModeSet set,
        out MapId mapId,
        out EntityUid mapEntity,
        out string mapName,
        out List<EntityUid> gridUids,
        out int entityCount,
        out string message)
    {
        mapId = MapId.Nullspace;
        mapEntity = EntityUid.Invalid;
        mapName = string.Empty;
        gridUids = new List<EntityUid>();
        entityCount = 0;

        if (_activeWeeklyMapId == null && _state.ActiveWeeklyMapId is { } persistedMapId)
            _activeWeeklyMapId = new MapId(persistedMapId);

        if (_activeWeeklyMapId == null || _activeWeeklyMapId.Value == MapId.Nullspace)
        {
            message = "Weekly save aborted: no active Weekly map has been loaded for this round.";
            return false;
        }

        mapId = _activeWeeklyMapId.Value;
        if (!_map.MapExists(mapId))
        {
            message = $"Weekly save aborted: active Weekly map {mapId} no longer exists.";
            return false;
        }

        mapEntity = _activeWeeklyMapEntity ?? _map.GetMapOrInvalid(mapId);
        if (!mapEntity.IsValid() ||
            !_metaQuery.TryGetComponent(mapEntity, out var meta) ||
            meta.EntityLifeStage >= EntityLifeStage.Terminating)
        {
            message = $"Weekly save aborted: active Weekly map {mapId} is terminating or has no live map entity.";
            return false;
        }

        var activePrototype = _activeWeeklyMapPrototype ?? _state.ActiveWeeklyMapPrototype;
        if (!string.Equals(activePrototype, set.BaseMapPrototype, StringComparison.Ordinal))
        {
            message =
                "Weekly save aborted:\n" +
                "active map does not match the map assigned to this Weekly set.\n" +
                $"Expected: {set.BaseMapPrototype}\n" +
                $"Actual: {activePrototype ?? "<unknown>"}";
            return false;
        }

        mapName = string.IsNullOrWhiteSpace(meta.EntityName)
            ? _activeWeeklyMapName ?? _state.ActiveWeeklyMapName ?? mapId.ToString()
            : meta.EntityName;

        if (IsDevMapMismatch(set.BaseMapPrototype, mapName))
        {
            message =
                "Weekly save aborted:\n" +
                "active map does not match the map assigned to this Weekly set.\n" +
                $"Expected: {set.BaseMapPrototype}\n" +
                $"Actual: {mapName}";
            return false;
        }

        gridUids = CollectGridUids(mapId);
        if (gridUids.Count == 0 && _activeWeeklyGridIds.Count > 0)
            gridUids.AddRange(_activeWeeklyGridIds.Where(uid => uid.IsValid()));

        entityCount = CountEntitiesOnMap(mapId, mapEntity, gridUids);
        message = string.Empty;
        return true;
    }

    private bool TryLoadCompatibleSnapshot(
        WeeklyModeSet set,
        string snapshotId,
        [NotNullWhen(true)] out WeeklySnapshotMetadata? metadata,
        out string message)
    {
        metadata = null;

        if (!_store.TryLoadSnapshot(set.SetId, snapshotId, out metadata))
        {
            message = $"Snapshot '{snapshotId}' was not found in set '{set.SetId}'.";
            return false;
        }

        if (!string.Equals(metadata.SetId, set.SetId, StringComparison.Ordinal))
        {
            message = $"Snapshot '{snapshotId}' belongs to set '{metadata.SetId}', not '{set.SetId}'.";
            return false;
        }

        if (!string.Equals(metadata.BaseMapPrototype, set.BaseMapPrototype, StringComparison.Ordinal))
        {
            message =
                "Snapshot map mismatch:\n" +
                $"set base map: {set.BaseMapPrototype}\n" +
                $"snapshot map: {metadata.BaseMapPrototype}";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(metadata.BaseMapPath) &&
            !string.IsNullOrWhiteSpace(set.BaseMapPath) &&
            !string.Equals(metadata.BaseMapPath, set.BaseMapPath, StringComparison.Ordinal))
        {
            message =
                "Snapshot map path mismatch:\n" +
                $"set base map path: {set.BaseMapPath}\n" +
                $"snapshot map path: {metadata.BaseMapPath}";
            return false;
        }

        if (!TryReadSnapshotStationMapName(set.SetId, snapshotId, out var snapshotMapName, out message))
        {
            message = $"Snapshot '{snapshotId}' cannot be validated: {message}";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(metadata.SavedMapName) &&
            !string.Equals(metadata.SavedMapName, snapshotMapName, StringComparison.Ordinal))
        {
            message =
                "Snapshot map mismatch:\n" +
                $"metadata map: {metadata.SavedMapName}\n" +
                $"station map: {snapshotMapName}";
            return false;
        }

        if (IsDevMapMismatch(set.BaseMapPrototype, snapshotMapName))
        {
            message =
                "Snapshot map mismatch:\n" +
                $"set base map: {set.BaseMapPrototype}\n" +
                $"snapshot map: {snapshotMapName}";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool TryReadSnapshotStationMapName(
        string setId,
        string snapshotId,
        [NotNullWhen(true)] out string? mapName,
        out string message)
    {
        mapName = null;
        var stationPath = _store.SnapshotStationPath(setId, snapshotId);

        if (!_mapLoader.TryReadFile(stationPath, out var data))
        {
            message = $"could not read {stationPath}.";
            return false;
        }

        return TryReadSerializedMapName(data, out mapName, out message);
    }

    internal static bool TryReadSerializedMapName(
        MappingDataNode data,
        [NotNullWhen(true)] out string? mapName,
        out string message)
    {
        mapName = null;
        var mapYamlIds = new HashSet<int>();

        if (data.TryGet<SequenceDataNode>("maps", out var maps))
        {
            foreach (var node in maps)
            {
                if (node is ValueDataNode value && int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                    mapYamlIds.Add(id);
            }
        }

        foreach (var entity in EnumerateSerializedEntities(data))
        {
            if (!TryGetSerializedEntityUid(entity, out var uid))
                continue;

            if (mapYamlIds.Count > 0 && !mapYamlIds.Contains(uid))
                continue;

            if (!TryGetSerializedComponent(entity, "Map", out _))
                continue;

            if (TryGetSerializedMetaName(entity, out mapName))
            {
                message = string.Empty;
                return true;
            }

            message = $"serialized map entity {uid} has no MetaData.name.";
            return false;
        }

        if (mapYamlIds.Count > 0)
        {
            message = $"serialized map entity {string.Join(",", mapYamlIds.OrderBy(id => id))} was not found.";
            return false;
        }

        message = "serialized map root was not found.";
        return false;
    }

    private static IEnumerable<MappingDataNode> EnumerateSerializedEntities(MappingDataNode data)
    {
        if (!data.TryGet<SequenceDataNode>("entities", out var groups))
            yield break;

        foreach (var groupNode in groups)
        {
            if (groupNode is not MappingDataNode group ||
                !group.TryGet<SequenceDataNode>("entities", out var entities))
            {
                continue;
            }

            foreach (var entityNode in entities)
            {
                if (entityNode is MappingDataNode entity)
                    yield return entity;
            }
        }
    }

    private static bool TryGetSerializedEntityUid(MappingDataNode entity, out int uid)
    {
        uid = 0;
        return entity.TryGet<ValueDataNode>("uid", out var uidNode) &&
               int.TryParse(uidNode.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uid);
    }

    private static bool TryGetSerializedComponent(
        MappingDataNode entity,
        string componentType,
        [NotNullWhen(true)] out MappingDataNode? component)
    {
        component = null;
        if (!entity.TryGet<SequenceDataNode>("components", out var components))
            return false;

        foreach (var componentNode in components)
        {
            if (componentNode is not MappingDataNode candidate ||
                !candidate.TryGet<ValueDataNode>("type", out var typeNode) ||
                !string.Equals(typeNode.Value, componentType, StringComparison.Ordinal))
            {
                continue;
            }

            component = candidate;
            return true;
        }

        return false;
    }

    private static bool TryGetSerializedMetaName(MappingDataNode entity, [NotNullWhen(true)] out string? mapName)
    {
        mapName = null;
        if (!TryGetSerializedComponent(entity, "MetaData", out var meta) ||
            !meta.TryGet<ValueDataNode>("name", out var nameNode) ||
            string.IsNullOrWhiteSpace(nameNode.Value))
        {
            return false;
        }

        mapName = nameNode.Value;
        return true;
    }

    private List<EntityUid> CollectGridUids(MapId mapId)
    {
        var result = new List<EntityUid>();
        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var xform, out var meta))
        {
            if (xform.MapID == mapId && meta.EntityLifeStage < EntityLifeStage.Terminating)
                result.Add(uid);
        }

        return result;
    }

    private int CountEntitiesOnMap(MapId mapId, EntityUid mapEntity, IReadOnlyCollection<EntityUid> gridUids)
    {
        var count = 0;
        foreach (var uid in CollectWeeklyMapEntities(mapEntity, gridUids))
        {
            if (!_metaQuery.TryGetComponent(uid, out var meta) ||
                meta.EntityLifeStage >= EntityLifeStage.Terminating)
                continue;

            count++;
        }

        return count;
    }

    private string GetCurrentOrConfiguredMapName()
    {
        var defaultMap = _gameTicker.DefaultMap;
        if (defaultMap != MapId.Nullspace && _map.MapExists(defaultMap))
        {
            var mapEntity = _map.GetMapOrInvalid(defaultMap);
            if (_metaQuery.TryGetComponent(mapEntity, out var meta) &&
                !string.IsNullOrWhiteSpace(meta.EntityName))
            {
                return meta.EntityName;
            }
        }

        return _gameMapManager.GetSelectedMap()?.MapName ?? "<none>";
    }

    private void ClearActiveWeeklyMapTracking()
    {
        _activeWeeklyMapId = null;
        _activeWeeklyMapEntity = null;
        _activeWeeklyGridIds.Clear();
        _activeWeeklyMapPrototype = null;
        _activeWeeklyMapName = null;
        _state.ActiveWeeklyMapId = null;
        _state.ActiveWeeklyMapEntity = null;
        _state.ActiveWeeklyGridIds = new List<int>();
        _state.ActiveWeeklyMapPrototype = null;
        _state.ActiveWeeklyMapName = null;
    }

    private static bool IsDevMapMismatch(string baseMapPrototype, string? mapName)
    {
        return !string.Equals(baseMapPrototype, "Dev", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(mapName, "Dev", StringComparison.OrdinalIgnoreCase);
    }

    private void SuppressSnapshotMapInitOnlyComponents(
        MapId mapId,
        EntityUid mapEntity,
        IReadOnlyCollection<EntityUid> gridUids)
    {
        var weeklyEntities = CollectWeeklyMapEntities(mapEntity, gridUids);
        var removed = 0;
        removed += RemoveMapInitOnlyComponents<ContainerFillComponent>(weeklyEntities);
        removed += RemoveMapInitOnlyComponents<EntityTableContainerFillComponent>(weeklyEntities);
        removed += RemoveMapInitOnlyComponents<StorageFillComponent>(weeklyEntities);
        removed += RemoveMapInitOnlyComponents<RandomFillSolutionComponent>(weeklyEntities);
        removed += RemoveMapInitOnlyComponents<ConditionalSpawnerComponent>(weeklyEntities);
        removed += RemoveMapInitOnlyComponents<RandomSpawnerComponent>(weeklyEntities);
        removed += RemoveMapInitOnlyComponents<EntityTableSpawnerComponent>(weeklyEntities);
        removed += RemoveMapInitOnlyComponents<RandomDecalSpawnerComponent>(weeklyEntities);

        if (removed > 0)
            _sawmill.Info($"Suppressed {removed} map-init-only fill components before initializing weekly snapshot map {mapId}.");
    }

    private int RemoveMapInitOnlyComponents<T>(HashSet<EntityUid> weeklyEntities) where T : IComponent
    {
        var removed = 0;
        foreach (var uid in weeklyEntities)
        {
            if (!HasComp<T>(uid))
                continue;

            RemComp<T>(uid);
            removed++;
        }

        return removed;
    }

    private void SuppressSnapshotStartingItems(
        MapId mapId,
        EntityUid mapEntity,
        IReadOnlyCollection<EntityUid> gridUids)
    {
        var weeklyEntities = CollectWeeklyMapEntities(mapEntity, gridUids);
        var poweredLightStartupLamps = 0;
        var itemSlotStartingItems = 0;
        var actionGrantStartingActions = 0;
        var binInitialContents = 0;
        var cartridgePreinstalledPrograms = 0;
        var lightReplacerStartingContents = 0;
        var vendingInitialStocks = 0;

        foreach (var uid in weeklyEntities)
        {
            if (TryComp<PoweredLightComponent>(uid, out var poweredLight) &&
                _poweredLight.ClearSpawnedPrototype((uid, poweredLight)))
            {
                poweredLightStartupLamps++;
            }

            if (TryComp<ItemSlotsComponent>(uid, out var itemSlots))
            {
                var itemSlotsChanged = false;
                foreach (var slot in itemSlots.Slots.Values)
                {
                    if (string.IsNullOrEmpty(slot.StartingItem))
                        continue;

                    slot.StartingItem = null;
                    itemSlotsChanged = true;
                    itemSlotStartingItems++;
                }

                if (itemSlotsChanged)
                    Dirty(uid, itemSlots);
            }

            if (TryComp<ActionGrantComponent>(uid, out var actionGrant))
                actionGrantStartingActions += _actionGrant.SuppressMapInitGrants((uid, actionGrant));

            if (TryComp<BinComponent>(uid, out var bin))
                binInitialContents += _bin.SuppressInitialContents((uid, bin));

            if (TryComp<CartridgeLoaderComponent>(uid, out var cartridgeLoader))
            {
                var suppressed = cartridgeLoader.PreinstalledPrograms.Count;
                if (suppressed > 0)
                {
                    cartridgeLoader.PreinstalledPrograms.Clear();
                    Dirty(uid, cartridgeLoader);
                    cartridgePreinstalledPrograms += suppressed;
                }
            }

            if (TryComp<LightReplacerComponent>(uid, out var lightReplacer))
                lightReplacerStartingContents += _lightReplacer.SuppressStartingContents((uid, lightReplacer));

            if (TryComp<VendingMachineComponent>(uid, out var vending) &&
                !vending.SuppressInitialRestock)
            {
                vending.SuppressInitialRestock = true;
                Dirty(uid, vending);
                vendingInitialStocks++;
            }
        }

        if (poweredLightStartupLamps > 0 ||
            itemSlotStartingItems > 0 ||
            actionGrantStartingActions > 0 ||
            binInitialContents > 0 ||
            cartridgePreinstalledPrograms > 0 ||
            lightReplacerStartingContents > 0 ||
            vendingInitialStocks > 0)
        {
            _sawmill.Info(
                "Suppressed snapshot starting items before initializing weekly snapshot map:\n" +
                $"map={mapId}\n" +
                $"poweredLightStartupLamps={poweredLightStartupLamps}\n" +
                $"itemSlotStartingItems={itemSlotStartingItems}\n" +
                $"actionGrantStartingActions={actionGrantStartingActions}\n" +
                $"binInitialContents={binInitialContents}\n" +
                $"cartridgePreinstalledPrograms={cartridgePreinstalledPrograms}\n" +
                $"lightReplacerStartingContents={lightReplacerStartingContents}\n" +
                $"vendingInitialStocks={vendingInitialStocks}");
        }
    }

    private void ApplyContainerPatch(
        MapId mapId,
        EntityUid mapEntity,
        IReadOnlyCollection<EntityUid> gridUids,
        WeeklyContainerPatch patch,
        string setId,
        string snapshotId)
    {
        var yamlToEntity = BuildYamlEntityMap(mapId, mapEntity, gridUids);
        var restored = 0;
        var skipped = 0;

        foreach (var entry in patch.Entries
                     .OrderBy(x => x.OwnerDepth)
                     .ThenBy(x => x.OwnerYamlUid)
                     .ThenBy(x => x.ContainerId, StringComparer.Ordinal)
                     .ThenBy(x => x.Index))
        {
            if (!yamlToEntity.TryGetValue(entry.OwnerYamlUid, out var owner))
            {
                skipped++;
                _sawmill.Warning($"Container patch skipped: owner yaml uid {entry.OwnerYamlUid} missing in snapshot '{snapshotId}'.");
                continue;
            }

            if (!yamlToEntity.TryGetValue(entry.ChildYamlUid, out var child))
            {
                skipped++;
                _sawmill.Warning($"Container patch skipped: child yaml uid {entry.ChildYamlUid} missing in snapshot '{snapshotId}'.");
                continue;
            }

            if (!_containers.TryGetContainer(owner, entry.ContainerId, out var container))
            {
                skipped++;
                _sawmill.Warning($"Container patch skipped: container '{entry.ContainerId}' missing on {ToPrettyString(owner)} in snapshot '{snapshotId}'.");
                continue;
            }

            if (container.Contains(child))
            {
                restored++;
                continue;
            }

            try
            {
                if (_containers.TryGetContainingContainer((child, null, null), out var currentContainer) &&
                    currentContainer != container)
                {
                    _containers.Remove((child, null, null), currentContainer, reparent: false, force: true);
                }

                if (_containers.Insert((child, null, null), container, force: true))
                {
                    restored++;
                    continue;
                }

                skipped++;
                _sawmill.Warning($"Container patch skipped: failed to insert {ToPrettyString(child)} into '{entry.ContainerId}' on {ToPrettyString(owner)}.");
            }
            catch (Exception e)
            {
                skipped++;
                _sawmill.Warning($"Container patch skipped corrupt entry owner={entry.OwnerYamlUid} child={entry.ChildYamlUid} container={entry.ContainerId}: {e.Message}");
            }
        }

        _sawmill.Info($"Applied weekly container patch for set '{setId}' snapshot '{snapshotId}': restored={restored}, skipped={skipped}, entries={patch.Entries.Count}.");
    }

    private void DeactivateLoadedSnapshotBorgs(EntityUid mapEntity, IReadOnlyCollection<EntityUid> gridUids)
    {
        var weeklyEntities = CollectWeeklyMapEntities(mapEntity, gridUids);
        var deactivated = 0;
        foreach (var uid in weeklyEntities)
        {
            if (!TryComp<BorgChassisComponent>(uid, out var borg))
                continue;

            if (borg.Active)
                _borg.SetActive((uid, borg), false);

            if (TryComp<MindContainerComponent>(uid, out var mindContainer) &&
                mindContainer.Mind != null)
            {
                _mind.TransferTo(mindContainer.Mind.Value, null, createGhost: false);
            }

            deactivated++;
        }

        if (deactivated > 0)
            _sawmill.Info($"Deactivated {deactivated} borg chassis after Weekly snapshot load.");
    }

    private Dictionary<int, EntityUid> BuildYamlEntityMap(
        MapId mapId,
        EntityUid mapEntity,
        IReadOnlyCollection<EntityUid> gridUids)
    {
        var result = new Dictionary<int, EntityUid>();
        foreach (var uid in CollectWeeklyMapEntities(mapEntity, gridUids))
        {
            if (!_yamlUidQuery.TryGetComponent(uid, out var yaml))
                continue;

            result.TryAdd(yaml.Uid, uid);
        }

        return result;
    }

    private HashSet<EntityUid> CollectWeeklyMapEntities(EntityUid mapEntity, IReadOnlyCollection<EntityUid> gridUids)
    {
        var entities = new HashSet<EntityUid>();
        var toVisit = new List<EntityUid>();
        if (mapEntity.IsValid())
            toVisit.Add(mapEntity);

        foreach (var grid in gridUids)
        {
            if (grid.IsValid())
                toVisit.Add(grid);
        }

        for (var i = 0; i < toVisit.Count; i++)
        {
            var uid = toVisit[i];
            if (!entities.Add(uid) ||
                !_xformQuery.TryGetComponent(uid, out var xform))
            {
                continue;
            }

            using var children = xform.ChildEnumerator;
            while (children.MoveNext(out var child))
                toVisit.Add(child);
        }

        return entities;
    }

    private void ApplyRoleOverridesToStations(WeeklyModeSet set)
    {
        var query = EntityQueryEnumerator<StationJobsComponent>();
        while (query.MoveNext(out var station, out _))
        {
            ApplyRoleOverridesToStation(station, set);
        }
    }

    private void ApplyRoleOverridesToStation(EntityUid station, WeeklyModeSet set)
    {
        var disabledJobs = set.DefaultDisabledJobs.ToHashSet(StringComparer.Ordinal);
        foreach (var jobId in set.DefaultDisabledJobs)
        {
            if (!_stationJobs.TryGetJobSlot(station, jobId, out var current))
                continue;

            var stationSlots = _originalSlots.GetOrNew(station);
            stationSlots.TryAdd(jobId, current);
            _stationJobs.TrySetJobSlot(station, jobId, 0);
        }

        foreach (var (jobId, limit) in set.DefaultRoleLimits.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (disabledJobs.Contains(jobId) || limit < 0)
                continue;

            if (!_stationJobs.TryGetJobSlot(station, jobId, out var current))
                continue;

            var stationSlots = _originalSlots.GetOrNew(station);
            stationSlots.TryAdd(jobId, current);
            _stationJobs.TrySetJobSlot(station, jobId, limit);
        }
    }

    private void RestoreRolesOnStations(IReadOnlyList<string> jobIds)
    {
        if (jobIds.Count == 0)
            return;

        foreach (var (station, slots) in _originalSlots.ToArray())
        {
            foreach (var jobId in jobIds)
            {
                if (!slots.TryGetValue(jobId, out var original))
                    continue;

                if (original == null)
                    _stationJobs.MakeJobUnlimited(station, jobId);
                else
                    _stationJobs.TrySetJobSlot(station, jobId, original.Value, true);

                slots.Remove(jobId);
            }

            if (slots.Count == 0)
                _originalSlots.Remove(station);
        }
    }

    private void RestoreAllRolesOnStations()
    {
        foreach (var (station, slots) in _originalSlots.ToArray())
        {
            foreach (var (jobId, original) in slots.ToArray())
            {
                if (original == null)
                    _stationJobs.MakeJobUnlimited(station, jobId);
                else
                    _stationJobs.TrySetJobSlot(station, jobId, original.Value, true);
            }
        }

        _originalSlots.Clear();
    }

    private void ApplyWeeklyResearchOverlay(WeeklyModeSet set)
    {
        var technologies = BuildWeeklyResearchData(set);

        var query = EntityQueryEnumerator<TechnologyDatabaseComponent>();
        while (query.MoveNext(out var uid, out var database))
            _research.SetWeeklyModeOverlay(uid, true, technologies, true, database);

        RaiseLocalEvent(new WeeklyRecipesChangedEvent());
    }

    private void RestoreWeeklyResearchOverlay()
    {
        var query = EntityQueryEnumerator<TechnologyDatabaseComponent>();
        while (query.MoveNext(out var uid, out var database))
        {
            if (!database.WeeklyModeOnly &&
                database.WeeklyAllowedTechnologies.Count == 0 &&
                database.WeeklyTechnologies.Count == 0 &&
                database.WeeklyUnlockedTechnologies.Count == 0)
            {
                continue;
            }

            _research.SetWeeklyModeOverlay(uid, false, Array.Empty<WeeklyTechnologyData>(), false, database);
        }
    }

    private List<WeeklyTechnologyData> BuildWeeklyResearchData(WeeklyModeSet set)
    {
        var technologies = new List<WeeklyTechnologyData>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in set.WeeklyTechnologies)
        {
            if (!seen.Add(entry.TechnologyId))
                continue;

            if (!TryNormalizeTechnologyBranch(entry.Branch, out var branch, out _) ||
                !WeeklyTechnologyDisciplineIds.TryGetValue(branch, out var disciplineId) ||
                !_prototype.TryIndex<TechDisciplinePrototype>(disciplineId, out var discipline))
            {
                continue;
            }

            var name = entry.TechnologyId;
            var icon = discipline.Icon;
            if (_prototype.TryIndex<TechnologyPrototype>(entry.TechnologyId, out var existingTechnology))
            {
                name = Loc.GetString(existingTechnology.Name);
                icon = existingTechnology.Icon;
            }

            technologies.Add(new WeeklyTechnologyData
            {
                TechnologyId = entry.TechnologyId,
                Name = name,
                Branch = disciplineId,
                Cost = entry.Cost,
                Tier = entry.Tier,
                RecipeIds = entry.RecipeIds.Distinct(StringComparer.Ordinal).ToList(),
                Icon = icon,
            });
        }

        technologies.Sort((a, b) => string.Compare(a.TechnologyId, b.TechnologyId, StringComparison.Ordinal));
        return technologies;
    }

    internal SnapshotExclusionResult CollectSnapshotExclusionsForTest(WeeklyModeSet set)
    {
        return CollectSnapshotExclusions(set);
    }

    private SnapshotExclusionResult CollectSnapshotExclusions(WeeklyModeSet set)
    {
        var excluded = new HashSet<EntityUid>();
        var forceMapSavablePrototypes = new HashSet<string>(StringComparer.Ordinal);
        var excludedPrototypes = set.ExcludedMobPrototypes.ToHashSet(StringComparer.Ordinal);
        var autonomousMobsIncluded = 0;
        var autonomousMobsExcluded = 0;
        var playerBodiesExcluded = 0;
        var borgChassisIncluded = 0;
        var borgChassisExcluded = 0;

        void AddExcludedPlayerControlled(EntityUid uid)
        {
            if (!excluded.Add(uid))
                return;

            if (HasComp<BorgChassisComponent>(uid))
                borgChassisExcluded++;
            else if (HasComp<MobStateComponent>(uid))
                playerBodiesExcluded++;
        }

        void AddExcludedAutonomousMob(EntityUid uid)
        {
            if (excluded.Add(uid))
                autonomousMobsExcluded++;
        }

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } attached)
                continue;

            if (!ShouldPreservePlayerControlledEntity(attached, set))
                AddExcludedPlayerControlled(attached);
        }

        var actors = EntityQueryEnumerator<ActorComponent>();
        while (actors.MoveNext(out var uid, out _))
        {
            if (!ShouldPreservePlayerControlledEntity(uid, set))
                AddExcludedPlayerControlled(uid);
        }

        var ghosts = EntityQueryEnumerator<GhostComponent>();
        while (ghosts.MoveNext(out var uid, out _))
            excluded.Add(uid);

        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var uid, out _))
            excluded.Add(uid);

        var mindContainers = EntityQueryEnumerator<MindContainerComponent>();
        while (mindContainers.MoveNext(out var uid, out var mindContainer))
        {
            if (mindContainer.HasMind || mindContainer.Mind != null)
            {
                if (!ShouldPreservePlayerControlledEntity(uid, set))
                    AddExcludedPlayerControlled(uid);
            }
        }

        var mobs = EntityQueryEnumerator<MobStateComponent>();
        while (mobs.MoveNext(out var uid, out _))
        {
            if (excluded.Contains(uid))
                continue;

            var prototypeId = GetPrototypeId(uid);
            var hasPrototype = !string.IsNullOrWhiteSpace(prototypeId);
            if (hasPrototype &&
                IsPrototypeOrParentExcluded(prototypeId, excludedPrototypes))
            {
                if (HasComp<BorgChassisComponent>(uid))
                {
                    if (excluded.Add(uid))
                        borgChassisExcluded++;
                }
                else
                {
                    AddExcludedAutonomousMob(uid);
                }

                continue;
            }

            if (ShouldPreservePlayerControlledEntity(uid, set))
            {
                if (hasPrototype)
                    forceMapSavablePrototypes.Add(prototypeId);

                borgChassisIncluded++;
                continue;
            }

            if (IsPlayerControlledEntity(uid))
            {
                AddExcludedPlayerControlled(uid);
                continue;
            }

            if (!set.PersistAutonomousMobs)
            {
                AddExcludedAutonomousMob(uid);
                continue;
            }

            if (hasPrototype)
                forceMapSavablePrototypes.Add(prototypeId);

            autonomousMobsIncluded++;
        }

        return new SnapshotExclusionResult(
            excluded,
            forceMapSavablePrototypes,
            autonomousMobsIncluded,
            autonomousMobsExcluded,
            playerBodiesExcluded,
            borgChassisIncluded,
            borgChassisExcluded);
    }

    private Dictionary<EntityPrototype, bool> ForceMapSavablePrototypes(IReadOnlySet<string> prototypeIds)
    {
        var changed = new Dictionary<EntityPrototype, bool>();
        foreach (var prototypeId in prototypeIds)
        {
            if (!_prototype.TryIndex<EntityPrototype>(prototypeId, out var prototype))
                continue;

            if (prototype.MapSavable)
                continue;

            changed[prototype] = prototype.MapSavable;
            prototype.MapSavable = true;
        }

        return changed;
    }

    private static void RestoreMapSavablePrototypes(Dictionary<EntityPrototype, bool> changed)
    {
        foreach (var (prototype, previous) in changed)
        {
            prototype.MapSavable = previous;
        }
    }

    private bool ShouldPreservePlayerControlledEntity(EntityUid uid, WeeklyModeSet set)
    {
        return set.PersistPlayerControlledBorgs && HasComp<BorgChassisComponent>(uid);
    }

    private bool IsPlayerControlledEntity(EntityUid uid)
    {
        if (_actorQuery.HasComponent(uid))
            return true;

        if (_mindContainerQuery.TryGetComponent(uid, out var mindContainer) &&
            (mindContainer.HasMind || mindContainer.Mind != null))
        {
            return true;
        }

        return _playerManager.Sessions.Any(session => session.AttachedEntity == uid);
    }

    private bool IsPrototypeOrParentExcluded(string prototypeId, IReadOnlySet<string> excludedPrototypes)
    {
        if (excludedPrototypes.Contains(prototypeId))
            return true;

        foreach (var (parentId, _) in _prototype.EnumerateAllParents<EntityPrototype>(prototypeId, true))
        {
            if (excludedPrototypes.Contains(parentId))
                return true;
        }

        return false;
    }

    private WeeklyContainerPatch BuildContainerPatch(
        IReadOnlyDictionary<EntityUid, int> yamlUidMap,
        HashSet<EntityUid> excludedRoots)
    {
        var patch = new WeeklyContainerPatch();
        var query = EntityQueryEnumerator<ContainerManagerComponent>();

        while (query.MoveNext(out var owner, out var manager))
        {
            if (IsEntityOrParentExcluded(owner, excludedRoots))
            {
                patch.SkippedExcluded++;
                continue;
            }

            if (!yamlUidMap.TryGetValue(owner, out var ownerYaml))
            {
                patch.SkippedUnserialized++;
                continue;
            }

            var ownerDepth = GetContainerOwnerDepth(owner);
            foreach (var (containerId, container) in manager.Containers.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var contained = container.ContainedEntities;
                for (var i = 0; i < contained.Count; i++)
                {
                    var child = contained[i];
                    if (IsEntityOrParentExcluded(child, excludedRoots))
                    {
                        patch.SkippedExcluded++;
                        continue;
                    }

                    if (!yamlUidMap.TryGetValue(child, out var childYaml))
                    {
                        patch.SkippedUnserialized++;
                        continue;
                    }

                    patch.Entries.Add(new WeeklyContainerEntry
                    {
                        OwnerYamlUid = ownerYaml,
                        OwnerPrototype = GetPrototypeId(owner),
                        ContainerId = containerId,
                        ChildYamlUid = childYaml,
                        ChildPrototype = GetPrototypeId(child),
                        Index = i,
                        OwnerDepth = ownerDepth,
                    });
                }
            }
        }

        patch.Entries.Sort((a, b) =>
        {
            var cmp = a.OwnerDepth.CompareTo(b.OwnerDepth);
            if (cmp != 0)
                return cmp;

            cmp = a.OwnerYamlUid.CompareTo(b.OwnerYamlUid);
            if (cmp != 0)
                return cmp;

            cmp = string.Compare(a.ContainerId, b.ContainerId, StringComparison.Ordinal);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        return patch;
    }

    private int GetContainerOwnerDepth(EntityUid owner)
    {
        var depth = 0;
        var current = owner;
        var guard = 0;

        while (current.IsValid() && guard++ < 256)
        {
            if (_containers.TryGetContainingContainer((current, null, null), out _))
                depth++;

            if (!_xformQuery.TryGetComponent(current, out var xform) || !xform.ParentUid.IsValid())
                break;

            current = xform.ParentUid;
        }

        return depth;
    }

    private string GetPrototypeId(EntityUid uid)
    {
        return _metaQuery.TryGetComponent(uid, out var meta)
            ? meta.EntityPrototype?.ID ?? string.Empty
            : string.Empty;
    }

    private bool IsCompleteSnapshotDirectory(ResPath snapshotDirectory, out string message)
    {
        var required = new[]
        {
            WeeklyModeStore.StationFileName,
            WeeklyModeStore.SnapshotMetadataFileName,
            WeeklyModeStore.RoleOverridesFileName,
            WeeklyModeStore.ContainerPatchFileName,
            WeeklyModeStore.IntegrityFileName,
        };

        foreach (var fileName in required)
        {
            var path = _store.SnapshotFilePath(snapshotDirectory, fileName);
            if (_resource.UserData.Exists(path))
                continue;

            message = $"Snapshot bundle is incomplete: missing {fileName}.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private WeeklySnapshotIntegrity BuildIntegrity(string setId, string snapshotId, DateTime createdAtUtc, ResPath snapshotDirectory)
    {
        var integrity = new WeeklySnapshotIntegrity
        {
            SetId = setId,
            SnapshotId = snapshotId,
            CreatedAtUtc = createdAtUtc,
        };

        foreach (var fileName in new[]
                 {
                     WeeklyModeStore.StationFileName,
                     WeeklyModeStore.SnapshotMetadataFileName,
                     WeeklyModeStore.RoleOverridesFileName,
                     WeeklyModeStore.ContainerPatchFileName,
                 })
        {
            var path = _store.SnapshotFilePath(snapshotDirectory, fileName);
            integrity.Files[fileName] = BuildFileIntegrity(path);
        }

        return integrity;
    }

    private WeeklySnapshotFileIntegrity BuildFileIntegrity(ResPath path)
    {
        var bytes = _resource.UserData.ReadAllBytes(path);
        return new WeeklySnapshotFileIntegrity
        {
            SizeBytes = bytes.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
        };
    }

    private bool TrySaveMapExcludingPlayers(
        MapId mapId,
        ResPath path,
        SnapshotExclusionResult snapshotExclusions,
        [NotNullWhen(true)] out Dictionary<EntityUid, int>? yamlUidMap)
    {
        yamlUidMap = null;
        var excludedRoots = snapshotExclusions.ExcludedRoots;
        void Filter(Entity<MetaDataComponent> ent, ref bool serializable)
        {
            if (!serializable)
                return;

            if (IsEntityOrParentExcluded(ent.Owner, excludedRoots))
                serializable = false;
        }

        var options = SerializationOptions.Default with
        {
            MissingEntityBehaviour = MissingEntityBehaviour.Ignore,
        };

        if (!_map.TryGetMap(mapId, out var mapUid))
        {
            _sawmill.Error($"Unable to find map {mapId} while saving weekly snapshot.");
            return false;
        }

        options.Category = FileCategory.Map;
        var serializer = new EntitySerializer(_dependency, options);
        serializer.OnIsSerializeable += Filter;
        var forcedMapSavable = new Dictionary<EntityPrototype, bool>();
        try
        {
            forcedMapSavable = ForceMapSavablePrototypes(snapshotExclusions.ForceMapSavablePrototypes);

            var roots = new HashSet<EntityUid> { mapUid.Value };
            var ev = new BeforeSerializationEvent(roots, new HashSet<MapId> { mapId }, FileCategory.Map);
            RaiseLocalEvent(ev);

            serializer.SerializeEntityRecursive(roots);
            var data = serializer.Write();
            var category = serializer.GetCategory();
            if (category != FileCategory.Map)
            {
                _sawmill.Error($"Failed to save weekly snapshot map {mapId} as a map. Output: {category}");
                return false;
            }

            WriteSerializedSnapshot(path, data);

            var ev2 = new AfterSerializationEvent(roots, data, category);
            RaiseLocalEvent(ev2);

            yamlUidMap = new Dictionary<EntityUid, int>(serializer.YamlUidMap);
            return true;
        }
        catch (Exception e)
        {
            _sawmill.Error($"Caught exception while trying to serialize weekly snapshot map {mapId}:\n{e}");
            return false;
        }
        finally
        {
            RestoreMapSavablePrototypes(forcedMapSavable);
            serializer.OnIsSerializeable -= Filter;
        }
    }

    private bool TrySanitizeSerializedSnapshot(
        ResPath stationPath,
        out SnapshotSanitizationResult result,
        out string message)
    {
        result = default;

        if (!_mapLoader.TryReadFile(stationPath, out var data))
        {
            message = $"Failed to read serialized snapshot map '{stationPath}' for sanitation.";
            return false;
        }

        result = SanitizeSerializedSnapshot(data, GetInheritedSnapshotMapInitOnlyComponents);
        if (result.Changed)
            WriteSerializedSnapshot(stationPath, data);

        message = string.Empty;
        return true;
    }

    internal static SnapshotSanitizationResult SanitizeSerializedSnapshot(MappingDataNode data)
    {
        return SanitizeSerializedSnapshot(data, null);
    }

    private static SnapshotSanitizationResult SanitizeSerializedSnapshot(
        MappingDataNode data,
        Func<string, IReadOnlyList<string>>? getInheritedMapInitOnlyComponents)
    {
        var removedStationMembers = 0;
        var removedSuitSensorReferences = 0;
        var removedInvalidContainerReferences = 0;
        var removedStaleBuckleReferences = 0;
        var removedStaleStrapReferences = 0;
        var removedStaleJointReferences = 0;
        var disabledBorgChassis = 0;
        var resetMapInitializationFields = 0;
        var resetMindContainers = 0;
        var suppressedMapInitOnlyComponents = 0;
        var suppressedStartingItems = 0;

        if (!data.TryGet<SequenceDataNode>("entities", out var prototypeGroups))
        {
            return new SnapshotSanitizationResult(
                removedStationMembers,
                removedSuitSensorReferences,
                removedInvalidContainerReferences,
                removedStaleBuckleReferences,
                removedStaleStrapReferences,
                removedStaleJointReferences,
                disabledBorgChassis,
                resetMapInitializationFields,
                resetMindContainers,
                suppressedMapInitOnlyComponents,
                suppressedStartingItems);
        }

        foreach (var groupNode in prototypeGroups)
        {
            if (groupNode is not MappingDataNode group ||
                !group.TryGet<SequenceDataNode>("entities", out var entities))
            {
                continue;
            }

            var protoId = group.TryGet<ValueDataNode>("proto", out var protoNode)
                ? protoNode.Value ?? string.Empty
                : string.Empty;
            var inheritedMapInitOnlyComponents = getInheritedMapInitOnlyComponents?.Invoke(protoId)
                                                 ?? Array.Empty<string>();

            foreach (var entityNode in entities)
            {
                if (entityNode is not MappingDataNode entity)
                    continue;

                entity.TryGet<SequenceDataNode>("components", out var components);

                if (entity.Remove("mapInit"))
                    resetMapInitializationFields++;
                if (entity.Remove("paused"))
                    resetMapInitializationFields++;

                List<string>? missingMapInitOnlyComponents = null;
                if (inheritedMapInitOnlyComponents.Count > 0)
                    missingMapInitOnlyComponents = new List<string>(inheritedMapInitOnlyComponents);

                for (var i = (components?.Count ?? 0) - 1; i >= 0; i--)
                {
                    if (components![i] is not MappingDataNode component ||
                        !TryGetSerializedComponentType(component, out var type))
                    {
                        continue;
                    }

                    if (IsSnapshotMapInitOnlyComponent(type))
                    {
                        components.RemoveAt(i);
                        missingMapInitOnlyComponents ??= new List<string>();
                        missingMapInitOnlyComponents.Add(type);
                        continue;
                    }

                    switch (type)
                    {
                        case "StationMember":
                            components.RemoveAt(i);
                            removedStationMembers++;
                            break;
                        case "Actor":
                            components.RemoveAt(i);
                            resetMindContainers++;
                            break;
                        case "MindContainer":
                            resetMindContainers += ResetSerializedMindContainer(component);
                            break;
                        case "BorgChassis":
                            disabledBorgChassis += DisableSerializedBorgChassis(component);
                            break;
                        case "SuitSensor":
                            if (component.Remove("station"))
                                removedSuitSensorReferences++;
                            if (component.Remove("user"))
                                removedSuitSensorReferences++;
                            break;
                        case "ContainerContainer":
                            removedInvalidContainerReferences += RemoveInvalidContainerReferences(component);
                            break;
                        case "Buckle":
                            removedStaleBuckleReferences += RemoveStaleBuckleReferences(component);
                            break;
                        case "Strap":
                            removedStaleStrapReferences += RemoveStaleStrapReferences(component);
                            break;
                        case "Joint":
                            removedStaleJointReferences += RemoveStaleJointReferences(component);
                            break;
                        case "PoweredLight":
                            if (SuppressPoweredLightStartupLamp(component))
                                suppressedStartingItems++;
                            break;
                        case "ItemSlots":
                            suppressedStartingItems += SuppressSerializedStartingItems(component);
                            break;
                        case "ActionGrant":
                            suppressedStartingItems += SuppressActionGrantStartingActions(component);
                            break;
                        case "Bin":
                            suppressedStartingItems += SuppressSequenceField(component, "initialContents");
                            break;
                        case "CartridgeLoader":
                            suppressedStartingItems += SuppressSequenceField(component, "preinstalled");
                            break;
                        case "LightReplacer":
                            suppressedStartingItems += SuppressEntitySpawnEntries(component, "contents");
                            break;
                        case "VendingMachine":
                            if (SuppressVendingInitialRestock(component))
                                suppressedStartingItems++;
                            break;
                        case "Map":
                            if (component.Remove("mapInitialized"))
                                resetMapInitializationFields++;
                            if (component.Remove("mapPaused"))
                                resetMapInitializationFields++;
                            break;
                    }
                }

                if (missingMapInitOnlyComponents is { Count: > 0 })
                {
                    suppressedMapInitOnlyComponents += AddMissingComponents(
                        entity,
                        missingMapInitOnlyComponents.Distinct(StringComparer.Ordinal));
                }
            }
        }

        return new SnapshotSanitizationResult(
            removedStationMembers,
            removedSuitSensorReferences,
            removedInvalidContainerReferences,
            removedStaleBuckleReferences,
            removedStaleStrapReferences,
            removedStaleJointReferences,
            disabledBorgChassis,
            resetMapInitializationFields,
            resetMindContainers,
            suppressedMapInitOnlyComponents,
            suppressedStartingItems);
    }

    private static int ResetSerializedMindContainer(MappingDataNode component)
    {
        var changed = 0;
        if (component.Remove("mind"))
            changed++;

        if (!component.TryGet<ValueDataNode>("hasMind", out var hasMind) ||
            !bool.TryParse(hasMind.Value, out var value) ||
            value)
        {
            component["hasMind"] = new ValueDataNode("false");
            changed++;
        }

        return changed;
    }

    private static int DisableSerializedBorgChassis(MappingDataNode component)
    {
        if (component.TryGet<ValueDataNode>("active", out var active) &&
            bool.TryParse(active.Value, out var value) &&
            !value)
        {
            return 0;
        }

        component["active"] = new ValueDataNode("false");
        return 1;
    }

    private static bool SuppressPoweredLightStartupLamp(MappingDataNode component)
    {
        if (component.TryGet<ValueDataNode>("hasLampOnSpawn", out var hasLampOnSpawn) &&
            hasLampOnSpawn.IsNull)
        {
            return false;
        }

        component["hasLampOnSpawn"] = ValueDataNode.Null();
        return true;
    }

    private static int SuppressSerializedStartingItems(DataNode node)
    {
        var suppressed = 0;
        switch (node)
        {
            case MappingDataNode mapping:
                foreach (var (key, value) in mapping.ToArray())
                {
                    if (key == "startingItem" &&
                        value is not ValueDataNode { IsNull: true })
                    {
                        mapping[key] = ValueDataNode.Null();
                        suppressed++;
                        continue;
                    }

                    suppressed += SuppressSerializedStartingItems(value);
                }

                break;
            case SequenceDataNode sequence:
                foreach (var value in sequence)
                    suppressed += SuppressSerializedStartingItems(value);
                break;
        }

        return suppressed;
    }

    private static int SuppressActionGrantStartingActions(MappingDataNode component)
    {
        return SuppressSequenceField(component, "actions", createWhenMissing: true);
    }

    private static bool SuppressVendingInitialRestock(MappingDataNode component)
    {
        if (component.TryGet<ValueDataNode>("suppressInitialRestock", out var suppressed) &&
            bool.TryParse(suppressed.Value, out var value) &&
            value)
        {
            return false;
        }

        component["suppressInitialRestock"] = new ValueDataNode("true");
        return true;
    }

    private static int SuppressSequenceField(
        MappingDataNode component,
        string field,
        bool createWhenMissing = false)
    {
        if (component.TryGet<SequenceDataNode>(field, out var sequence))
        {
            var suppressed = sequence.Count;
            sequence.Clear();
            return suppressed;
        }

        if (!createWhenMissing)
            return 0;

        component[field] = new SequenceDataNode();
        return 1;
    }

    private static int SuppressEntitySpawnEntries(MappingDataNode component, string field)
    {
        if (!component.TryGet<SequenceDataNode>(field, out var sequence))
            return 0;

        var suppressed = 0;
        foreach (var entry in sequence.OfType<MappingDataNode>())
        {
            if (entry.TryGet<ValueDataNode>("amount", out var amount) &&
                int.TryParse(amount.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                suppressed += Math.Max(parsed, 0);
            }
            else
            {
                suppressed++;
            }
        }

        sequence.Clear();
        return suppressed;
    }

    private IReadOnlyList<string> GetInheritedSnapshotMapInitOnlyComponents(string protoId)
    {
        if (string.IsNullOrWhiteSpace(protoId) ||
            !_prototype.TryIndex<EntityPrototype>(protoId, out var proto))
        {
            return Array.Empty<string>();
        }

        List<string>? result = null;
        foreach (var componentName in SnapshotMapInitOnlyComponents)
        {
            if (!proto.Components.ContainsKey(componentName))
                continue;

            result ??= new List<string>();
            result.Add(componentName);
        }

        return result != null ? result : Array.Empty<string>();
    }

    private static bool IsSnapshotMapInitOnlyComponent(string type)
    {
        return SnapshotMapInitOnlyComponents.Contains(type, StringComparer.Ordinal);
    }

    private static int AddMissingComponents(MappingDataNode entity, IEnumerable<string> componentNames)
    {
        if (!entity.TryGet<SequenceDataNode>("missingComponents", out var missingComponents))
        {
            missingComponents = new SequenceDataNode();
            entity.Add("missingComponents", missingComponents);
        }

        var existing = missingComponents
            .OfType<ValueDataNode>()
            .Select(node => node.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

        var added = 0;
        foreach (var componentName in componentNames)
        {
            if (!existing.Add(componentName))
                continue;

            missingComponents.Add(new ValueDataNode(componentName));
            added++;
        }

        if (missingComponents.Count == 0)
            entity.Remove("missingComponents");

        return added;
    }

    private static bool TryGetSerializedComponentType(MappingDataNode component, [NotNullWhen(true)] out string? type)
    {
        type = null;
        if (!component.TryGet<ValueDataNode>("type", out var typeNode) ||
            string.IsNullOrWhiteSpace(typeNode.Value))
        {
            return false;
        }

        type = typeNode.Value;
        return true;
    }

    private static int RemoveInvalidContainerReferences(DataNode node)
    {
        var removed = 0;
        switch (node)
        {
            case MappingDataNode mapping:
                foreach (var (key, value) in mapping.ToArray())
                {
                    if (key == "ent" && IsInvalidEntityReference(value))
                    {
                        mapping[key] = ValueDataNode.Null();
                        removed++;
                        continue;
                    }

                    removed += RemoveInvalidContainerReferences(value);
                }

                break;
            case SequenceDataNode sequence:
                for (var i = sequence.Count - 1; i >= 0; i--)
                {
                    if (IsInvalidEntityReference(sequence[i]))
                    {
                        sequence.RemoveAt(i);
                        removed++;
                        continue;
                    }

                    removed += RemoveInvalidContainerReferences(sequence[i]);
                }

                break;
        }

        return removed;
    }

    private static int RemoveStaleBuckleReferences(MappingDataNode component)
    {
        if (!component.TryGet<ValueDataNode>("buckledTo", out var buckledTo) ||
            !IsInvalidEntityReference(buckledTo))
        {
            return 0;
        }

        component["buckledTo"] = ValueDataNode.Null();
        component.Remove("buckleTime");
        return 1;
    }

    private static int RemoveStaleStrapReferences(MappingDataNode component)
    {
        if (!component.TryGet<SequenceDataNode>("buckledEntities", out var buckledEntities))
            return 0;

        var removed = 0;
        for (var i = buckledEntities.Count - 1; i >= 0; i--)
        {
            if (!IsInvalidEntityReference(buckledEntities[i]))
                continue;

            buckledEntities.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    private static int RemoveStaleJointReferences(MappingDataNode component)
    {
        var removed = 0;
        if (component.TryGet<ValueDataNode>("relay", out var relay) &&
            IsInvalidEntityReference(relay))
        {
            component["relay"] = ValueDataNode.Null();
            removed++;
        }

        if (!component.TryGet<MappingDataNode>("joints", out var joints))
            return removed;

        foreach (var (jointId, jointData) in joints.ToArray())
        {
            if (!ContainsInvalidEntityReference(jointData))
                continue;

            joints.Remove(jointId);
            removed++;
        }

        return removed;
    }

    private static bool ContainsInvalidEntityReference(DataNode node)
    {
        if (IsInvalidEntityReference(node))
            return true;

        switch (node)
        {
            case MappingDataNode mapping:
                foreach (var (_, value) in mapping.ToArray())
                {
                    if (ContainsInvalidEntityReference(value))
                        return true;
                }

                return false;
            case SequenceDataNode sequence:
                return sequence.Any(ContainsInvalidEntityReference);
            default:
                return false;
        }
    }

    private static bool IsInvalidEntityReference(DataNode node)
    {
        return node is ValueDataNode { Value: "invalid" };
    }

    private void WriteSerializedSnapshot(ResPath path, MappingDataNode data)
    {
        path = path.ToRootedPath();
        _resource.UserData.CreateDir(path.Directory);
        using var writer = _resource.UserData.OpenWriteText(path);
        var document = new YamlDocument(data.ToYaml());
        var stream = new YamlStream { document };
        stream.Save(new YamlMappingFix(new Emitter(writer)), false);
    }

    private bool IsEntityOrParentExcluded(EntityUid uid, HashSet<EntityUid> excludedRoots)
    {
        if (excludedRoots.Count == 0)
            return false;

        var current = uid;
        var depth = 0;
        while (current.IsValid() && depth++ < 256)
        {
            if (excludedRoots.Contains(current))
                return true;

            if (!_xformQuery.TryGetComponent(current, out var xform))
                return false;

            current = xform.ParentUid;
        }

        return false;
    }

    private bool TryBuildWeeklyRecipeDefinition(
        string recipeId,
        string resultPrototype,
        int resultAmount,
        double productionTimeSeconds,
        string latheTargets,
        IReadOnlyList<string> materialSpecs,
        out WeeklyRecipeDefinition recipe,
        out string message)
    {
        recipe = new WeeklyRecipeDefinition();

        if (!WeeklyModeStore.IsSafeId(recipeId))
        {
            message = "recipeId must contain only ASCII letters, digits, '-', '_' or '.'.";
            return false;
        }

        if (_prototype.HasIndex<LatheRecipePrototype>(recipeId))
        {
            message = $"Weekly recipeId '{recipeId}' conflicts with an existing global lathe recipe prototype.";
            return false;
        }

        resultPrototype = resultPrototype.Trim();
        if (!_prototype.TryIndex<EntityPrototype>(resultPrototype, out var entityPrototype))
        {
            message = $"Unknown result entity prototype: {resultPrototype}";
            return false;
        }

        if (entityPrototype.Abstract)
        {
            message = $"Result entity prototype is abstract and cannot be produced: {resultPrototype}";
            return false;
        }

        if (resultAmount < 1)
        {
            message = "resultAmount must be at least 1.";
            return false;
        }

        if (!double.IsFinite(productionTimeSeconds) || productionTimeSeconds <= 0)
        {
            message = "productionTimeSeconds must be greater than zero.";
            return false;
        }

        if (!TryNormalizeLatheTargets(latheTargets, out var targets, out message))
            return false;

        if (!TryParseRecipeMaterials(materialSpecs, out var materials, out message))
            return false;

        recipe = new WeeklyRecipeDefinition
        {
            Id = recipeId,
            ResultPrototype = resultPrototype,
            ResultAmount = resultAmount,
            ProductionTimeSeconds = productionTimeSeconds,
            LatheTargets = targets,
            Materials = materials,
        };
        message = string.Empty;
        return true;
    }

    private bool TryNormalizeLatheTargets(string rawTargets, out List<string> targets, out string message)
    {
        targets = new List<string>();
        foreach (var rawTarget in rawTargets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryNormalizeLatheTarget(rawTarget, out var target, out message))
                return false;

            if (!targets.Contains(target, StringComparer.Ordinal))
                targets.Add(target);
        }

        targets.Sort(StringComparer.Ordinal);
        if (targets.Count == 0)
        {
            message = "At least one lathe target must be specified.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool TryNormalizeLatheTarget(string rawTarget, out string target, out string message)
    {
        target = rawTarget.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(target))
        {
            message = "Lathe target may not be empty.";
            return false;
        }

        if (WeeklyRecipeTargetAliases.TryGetValue(target, out var alias))
        {
            foreach (var entityId in alias.Entities)
            {
                if (!IsValidLatheEntityPrototype(entityId))
                {
                    message = $"Weekly recipe target alias '{target}' references missing lathe entity prototype '{entityId}'.";
                    return false;
                }
            }

            foreach (var packId in alias.Packs)
            {
                if (!_prototype.HasIndex<LatheRecipePackPrototype>(packId))
                {
                    message = $"Weekly recipe target alias '{target}' references missing lathe recipe pack '{packId}'.";
                    return false;
                }
            }

            target = target.ToLowerInvariant();
            message = string.Empty;
            return true;
        }

        if (target.StartsWith("entity:", StringComparison.OrdinalIgnoreCase))
        {
            var entityId = target["entity:".Length..].Trim();
            if (!IsValidLatheEntityPrototype(entityId))
            {
                message = $"Unknown lathe entity target: entity:{entityId}";
                return false;
            }

            target = $"entity:{entityId}";
            message = string.Empty;
            return true;
        }

        if (target.StartsWith("pack:", StringComparison.OrdinalIgnoreCase))
        {
            var packId = target["pack:".Length..].Trim();
            if (!_prototype.HasIndex<LatheRecipePackPrototype>(packId))
            {
                message = $"Unknown lathe recipe pack target: pack:{packId}";
                return false;
            }

            target = $"pack:{packId}";
            message = string.Empty;
            return true;
        }

        message = $"Unknown lathe target '{target}'. Use an alias ({FormatList(WeeklyRecipeTargetAliases.Keys)}), entity:<LatheEntityPrototypeId>, or pack:<LatheRecipePackPrototypeId>.";
        return false;
    }

    private bool IsValidLatheEntityPrototype(string entityId)
    {
        return _prototype.TryIndex<EntityPrototype>(entityId, out var entityPrototype) &&
               !entityPrototype.Abstract &&
               entityPrototype.Components.ContainsKey("Lathe");
    }

    private bool TryParseRecipeMaterials(IReadOnlyList<string> materialSpecs, out Dictionary<string, int> materials, out string message)
    {
        materials = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var spec in materialSpecs)
        {
            var parts = spec.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                message = $"Material '{spec}' must use MaterialPrototypeId:Amount.";
                return false;
            }

            var materialId = parts[0];
            if (!_prototype.HasIndex<MaterialPrototype>(materialId))
            {
                message = $"Unknown material prototype: {materialId}";
                return false;
            }

            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                message = $"Material amount for '{materialId}' must be greater than zero.";
                return false;
            }

            materials[materialId] = materials.TryGetValue(materialId, out var existing)
                ? existing + amount
                : amount;
        }

        if (materials.Count == 0)
        {
            message = "At least one material must be specified.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static void SortRecipes(WeeklyRecipesConfig recipes)
    {
        recipes.Recipes.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.Ordinal));
        foreach (var recipe in recipes.Recipes)
        {
            recipe.LatheTargets = recipe.LatheTargets.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            recipe.Materials = recipe.Materials.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            recipe.TechnologyIds = recipe.TechnologyIds.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        }
    }

    private static void SyncRecipeTechnologyLinks(WeeklyModeSet set, WeeklyRecipesConfig recipes)
    {
        foreach (var recipe in recipes.Recipes)
        {
            recipe.TechnologyIds = set.WeeklyTechnologies
                .Where(technology => technology.RecipeIds.Contains(recipe.Id, StringComparer.Ordinal))
                .Select(technology => technology.TechnologyId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        SortRecipes(recipes);
    }

    private bool IsWeeklyRecipeInUse(string recipeId)
    {
        if (!_state.IsActive)
            return false;

        var query = EntityQueryEnumerator<LatheComponent>();
        while (query.MoveNext(out _, out var lathe))
        {
            if (lathe.CurrentRecipeIsWeekly &&
                string.Equals(lathe.CurrentRecipe, recipeId, StringComparison.Ordinal))
            {
                return true;
            }

            foreach (var batch in lathe.Queue)
            {
                if (batch.IsWeekly && string.Equals(batch.Recipe, recipeId, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private WeeklyRecipesConfig CloneRecipes(WeeklyRecipesConfig recipes)
    {
        return JsonSerializer.Deserialize<WeeklyRecipesConfig>(_store.ExportRecipesJson(recipes))
               ?? throw new InvalidOperationException("Failed to clone weekly recipe config.");
    }

    private bool TryLoadMutableSet(string setId, [NotNullWhen(true)] out WeeklyModeSet? set, out string message)
    {
        set = null;

        if (!WeeklyModeStore.IsSafeId(setId))
        {
            message = "Invalid setId. Use only ASCII letters, digits, '-', '_' or '.'.";
            return false;
        }

        if (!_store.TryLoadSet(setId, out set))
        {
            message = $"Weekly set '{setId}' was not found.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool TryLoadConfigEditableSet(string setId, [NotNullWhen(true)] out WeeklyModeSet? set, out string message)
    {
        return TryLoadMutableSet(setId, out set, out message);
    }

    private bool TryCommitConfigChange(WeeklyModeSet originalSet, WeeklyModeSet changedSet, WeeklyLiveConfigChange liveChange, out string message)
    {
        if (!TryValidateSetConfig(changedSet, out var errors))
        {
            message = $"Weekly set '{changedSet.SetId}' config is invalid:\n- {string.Join("\n- ", errors)}";
            return false;
        }

        var active = IsActiveSet(changedSet.SetId);
        var saved = false;
        try
        {
            _store.SaveSet(changedSet);
            saved = true;

            if (active)
                ApplyLiveConfigChange(changedSet, liveChange);

            message = string.Empty;
            return true;
        }
        catch (Exception e)
        {
            if (active && saved)
            {
                try
                {
                    _store.SaveSet(originalSet);
                    ApplyLiveConfigChange(originalSet, liveChange);
                }
                catch (Exception rollbackException)
                {
                    _sawmill.Error($"Failed to roll back live weekly config change for set '{changedSet.SetId}': {rollbackException}");
                }
            }

            message = $"Failed to apply weekly config change for set '{changedSet.SetId}': {e.Message}";
            return false;
        }
    }

    private bool TryCommitRecipesChange(WeeklyModeSet set, WeeklyRecipesConfig originalRecipes, WeeklyRecipesConfig changedRecipes, WeeklyLiveConfigChange liveChange, out string message)
    {
        var errors = ValidateWeeklyRecipes(set, changedRecipes).ToList();
        if (errors.Count > 0)
        {
            message = $"Weekly recipes for '{set.SetId}' are invalid:\n- {string.Join("\n- ", errors)}";
            return false;
        }

        var active = IsActiveSet(set.SetId);
        var saved = false;
        try
        {
            _store.SaveRecipes(set.SetId, changedRecipes);
            saved = true;

            if (active)
                ApplyLiveConfigChange(set, liveChange);

            message = string.Empty;
            return true;
        }
        catch (Exception e)
        {
            if (active && saved)
            {
                try
                {
                    _store.SaveRecipes(set.SetId, originalRecipes);
                    ApplyLiveConfigChange(set, liveChange);
                }
                catch (Exception rollbackException)
                {
                    _sawmill.Error($"Failed to roll back live weekly recipe change for set '{set.SetId}': {rollbackException}");
                }
            }

            message = $"Failed to apply weekly recipe change for set '{set.SetId}': {e.Message}";
            return false;
        }
    }

    private bool TryCommitResearchConfigChange(WeeklyModeSet originalSet, WeeklyModeSet changedSet, out string message)
    {
        var originalRecipes = _store.LoadRecipesOrDefault(changedSet.SetId);
        var changedRecipes = CloneRecipes(originalRecipes);
        SyncRecipeTechnologyLinks(changedSet, changedRecipes);
        return TryCommitConfigAndRecipesChange(originalSet, changedSet, originalRecipes, changedRecipes, WeeklyLiveConfigChange.Research | WeeklyLiveConfigChange.Recipes, out message);
    }

    private bool TryCommitConfigAndRecipesChange(
        WeeklyModeSet originalSet,
        WeeklyModeSet changedSet,
        WeeklyRecipesConfig originalRecipes,
        WeeklyRecipesConfig changedRecipes,
        WeeklyLiveConfigChange liveChange,
        out string message)
    {
        if (!TryValidateSetConfig(changedSet, out var setErrors))
        {
            message = $"Weekly set '{changedSet.SetId}' config is invalid:\n- {string.Join("\n- ", setErrors)}";
            return false;
        }

        var recipeErrors = ValidateWeeklyRecipes(changedSet, changedRecipes).ToList();
        if (recipeErrors.Count > 0)
        {
            message = $"Weekly recipes for '{changedSet.SetId}' are invalid:\n- {string.Join("\n- ", recipeErrors)}";
            return false;
        }

        var active = IsActiveSet(changedSet.SetId);
        var savedSet = false;
        var savedRecipes = false;
        try
        {
            _store.SaveSet(changedSet);
            savedSet = true;
            _store.SaveRecipes(changedSet.SetId, changedRecipes);
            savedRecipes = true;

            if (active)
                ApplyLiveConfigChange(changedSet, liveChange);

            message = string.Empty;
            return true;
        }
        catch (Exception e)
        {
            if (savedSet || savedRecipes)
            {
                try
                {
                    _store.SaveSet(originalSet);
                    _store.SaveRecipes(originalSet.SetId, originalRecipes);
                    if (active)
                        ApplyLiveConfigChange(originalSet, liveChange);
                }
                catch (Exception rollbackException)
                {
                    _sawmill.Error($"Failed to roll back weekly config and recipe change for set '{changedSet.SetId}': {rollbackException}");
                }
            }

            message = $"Failed to apply weekly config change for set '{changedSet.SetId}': {e.Message}";
            return false;
        }
    }

    private void ApplyLiveConfigChange(WeeklyModeSet set, WeeklyLiveConfigChange liveChange)
    {
        if ((liveChange & WeeklyLiveConfigChange.Research) != 0)
        {
            ApplyWeeklyResearchOverlay(set);
            _research.RefreshResearchConsoles();
        }

        if ((liveChange & WeeklyLiveConfigChange.Cargo) != 0)
        {
            RaiseLocalEvent(new WeeklyCargoCatalogChangedEvent());
        }

        if ((liveChange & WeeklyLiveConfigChange.Roles) != 0)
        {
            ApplyRoleOverridesToStations(set);
        }

        if ((liveChange & (WeeklyLiveConfigChange.Research | WeeklyLiveConfigChange.Recipes)) != 0)
        {
            RaiseLocalEvent(new WeeklyRecipesChangedEvent());
        }
    }

    private WeeklyModeSet CloneSet(WeeklyModeSet set)
    {
        return JsonSerializer.Deserialize<WeeklyModeSet>(_store.ExportSetJson(set))
               ?? throw new InvalidOperationException($"Failed to clone weekly set '{set.SetId}'.");
    }

    private bool HasPurchasedWeeklyTechnology(string setId, string technologyId)
    {
        return GetPurchasedWeeklyTechnologies(setId).Contains(technologyId);
    }

    private HashSet<string> GetPurchasedWeeklyTechnologies(string setId)
    {
        var purchased = new HashSet<string>(StringComparer.Ordinal);
        if (!IsActiveSet(setId))
            return purchased;

        var query = EntityQueryEnumerator<TechnologyDatabaseComponent>();
        while (query.MoveNext(out _, out var database))
        {
            if (!database.WeeklyModeOnly)
                continue;

            foreach (var technologyId in database.WeeklyUnlockedTechnologies)
                purchased.Add(technologyId);
        }

        return purchased;
    }

    private IEnumerable<string> ValidateForcedRoleAssignments(WeeklyModeSet set)
    {
        var errors = new List<string>();
        var seenPlayers = new HashSet<Guid>();
        var seenRecords = new HashSet<string>(StringComparer.Ordinal);
        var countsByJob = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var assignment in set.ForcedRoleAssignments)
        {
            if (!Guid.TryParse(assignment.PlayerNetUserId, out var userId))
            {
                errors.Add($"forced role assignment has invalid playerNetUserId '{assignment.PlayerNetUserId}'");
                continue;
            }

            if (!seenPlayers.Add(userId))
                errors.Add($"player '{assignment.PlayerNetUserId}' has more than one forced role assignment");

            var recordKey = $"{assignment.PlayerNetUserId}:{assignment.JobId}";
            if (!seenRecords.Add(recordKey))
                errors.Add($"duplicate forced role assignment '{recordKey}'");

            if (string.IsNullOrWhiteSpace(assignment.LastKnownCKey))
                errors.Add($"forced role assignment '{assignment.PlayerNetUserId}' has empty lastKnownCKey");

            if (!_prototype.TryIndex<JobPrototype>(assignment.JobId, out _))
                errors.Add($"forced role assignment for '{assignment.PlayerNetUserId}' has unknown job prototype '{assignment.JobId}'");

            if (set.DefaultDisabledJobs.Contains(assignment.JobId))
                errors.Add($"forced role assignment for '{assignment.PlayerNetUserId}' uses disabled job '{assignment.JobId}'");

            if (set.DefaultRoleLimits.TryGetValue(assignment.JobId, out var limit) && limit == 0)
                errors.Add($"forced role assignment for '{assignment.PlayerNetUserId}' uses job '{assignment.JobId}' with campaign role limit 0");

            countsByJob[assignment.JobId] = countsByJob.GetValueOrDefault(assignment.JobId) + 1;
        }

        foreach (var (jobId, count) in countsByJob)
        {
            if (set.DefaultRoleLimits.TryGetValue(jobId, out var limit) && limit >= 0 && count > limit)
                errors.Add($"forced role assignments for '{jobId}' exceed campaign role limit {limit}: {count}");
        }

        return errors;
    }

    private bool TryValidateSetConfig(WeeklyModeSet set, out List<string> errors)
    {
        errors = new List<string>();

        if (set.SchemaVersion != WeeklyModeSet.CurrentSchemaVersion)
            errors.Add($"schemaVersion {set.SchemaVersion} is not compatible with {WeeklyModeSet.CurrentSchemaVersion}");

        if (!WeeklyModeStore.IsSafeId(set.SetId))
            errors.Add("setId is not a safe weekly mode id");

        if (string.IsNullOrWhiteSpace(set.BaseMapPrototype))
            errors.Add("baseMapPrototype is empty");
        else if (!_prototype.TryIndex<GameMapPrototype>(set.BaseMapPrototype, out _))
            errors.Add($"unknown base map prototype '{set.BaseMapPrototype}'");

        if (!string.IsNullOrWhiteSpace(set.BaseMapPath))
        {
            if (!TryResolveBaseMapPath(set.BaseMapPath, out var resolvedMap, out _, out var mapError))
            {
                errors.Add(mapError);
            }
            else if (!string.Equals(resolvedMap.ID, set.BaseMapPrototype, StringComparison.Ordinal))
            {
                errors.Add($"baseMapPath '{set.BaseMapPath}' resolves to map prototype '{resolvedMap.ID}', not configured prototype '{set.BaseMapPrototype}'");
            }
        }

        if (set.AutosaveMinutes < 1)
            errors.Add("autosave interval must be at least 1 minute");

        if (set.AutosaveWarningMinutes < 0)
            errors.Add("autosave warning may not be negative");

        if (set.AutosaveWarningMinutes >= set.AutosaveMinutes)
            errors.Add("autosave warning must be shorter than the autosave interval");

        if (set.RetainAutosaves < 1)
            errors.Add("autosave retention must be at least 1");

        foreach (var jobId in set.DefaultDisabledJobs.Distinct(StringComparer.Ordinal))
        {
            if (!_prototype.TryIndex<JobPrototype>(jobId, out _))
                errors.Add($"unknown disabled job prototype '{jobId}'");
        }

        foreach (var (jobId, alias) in set.DefaultRoleAliases)
        {
            if (!_prototype.TryIndex<JobPrototype>(jobId, out _))
                errors.Add($"unknown aliased job prototype '{jobId}'");

            if (!TryNormalizeAlias(alias, out _, out var aliasError))
                errors.Add($"invalid alias for '{jobId}': {aliasError}");
        }

        foreach (var (jobId, limit) in set.DefaultRoleLimits)
        {
            if (!_prototype.TryIndex<JobPrototype>(jobId, out _))
                errors.Add($"unknown limited job prototype '{jobId}'");

            if (limit < 0)
                errors.Add($"role limit for '{jobId}' may not be negative");
        }

        foreach (var prototypeId in set.ExcludedMobPrototypes.Distinct(StringComparer.Ordinal))
        {
            if (!_prototype.TryIndex<EntityPrototype>(prototypeId, out _))
                errors.Add($"unknown excluded mob prototype '{prototypeId}'");
        }

        if (set.MinPlaytimeHours < 0)
            errors.Add("minimum playtime may not be negative");

        if (set.DiscordChannel.Any(char.IsControl))
            errors.Add("discord channel/link may not contain control characters");

        var weeklyRecipes = _store.LoadRecipesOrDefault(set.SetId);
        errors.AddRange(ValidateWeeklyRecipes(set, weeklyRecipes));
        errors.AddRange(ValidateWeeklyTechnologies(set, weeklyRecipes));
        errors.AddRange(ValidateWeeklyCargoProducts(set));
        errors.AddRange(ValidateForcedRoleAssignments(set));

        return errors.Count == 0;
    }

    private void DeactivateWeeklyMode()
    {
        RestoreAllRolesOnStations();
        _state.IsActive = false;
        _state.ActiveSetId = null;
        _state.ActiveSnapshotId = null;
        _state.PendingSnapshotId = null;
        _state.StartedAtUtc = null;
        _state.StartedBy = null;
        ClearActiveWeeklyMapTracking();
        _nextAutosaveAt = null;
        _autosaveWarningIssued = false;
        _accessLobbyNoticeSent.Clear();
        _accessNoticeCooldowns.Clear();
        _forcedRoleNoticeCooldowns.Clear();
        RestoreWeeklyResearchOverlay();
        RaiseLocalEvent(new WeeklyRecipesChangedEvent());
        _gameMapManager.ClearSelectedMap();
        _store.SaveState(_state);
    }

    private bool TryResolveBaseMapPath(
        string rawPath,
        [NotNullWhen(true)] out GameMapPrototype? baseMap,
        out ResPath mapPath,
        out string message)
    {
        baseMap = null;
        mapPath = default;

        if (!TryNormalizeContentMapPath(rawPath, out mapPath, out message))
            return false;

        if (!_resource.ContentFileExists(mapPath))
        {
            message = $"Map file '{mapPath}' does not exist in server resources.";
            return false;
        }

        var resolvedPath = mapPath;
        baseMap = _prototype.EnumeratePrototypes<GameMapPrototype>()
            .FirstOrDefault(map => string.Equals(map.MapPath.CanonPath, resolvedPath.CanonPath, StringComparison.Ordinal));

        if (baseMap == null)
        {
            message = $"Map file '{mapPath}' exists, but no GameMapPrototype references it. Add a map prototype for this file before creating a Weekly set.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool TryNormalizeContentMapPath(string rawPath, out ResPath path, out string message)
    {
        path = default;
        rawPath = rawPath.Trim().Trim('"').Replace('\\', '/');

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            message = "Map path may not be empty.";
            return false;
        }

        if (rawPath.Contains(':') || rawPath.StartsWith("//", StringComparison.Ordinal))
        {
            message = "Map path must be a resource path, not an operating-system absolute path.";
            return false;
        }

        if (!rawPath.StartsWith("/", StringComparison.Ordinal))
        {
            message = "Map path must start with '/', for example /Maps/saltern.yml.";
            return false;
        }

        if (!ResPath.IsValidPath(rawPath))
        {
            message = "Map path contains invalid separators.";
            return false;
        }

        path = new ResPath(rawPath).ToRootedPath();
        if (ContainsTraversal(path))
        {
            message = "Map path may not contain '..'.";
            return false;
        }

        if (!string.Equals(path.Extension, "yml", StringComparison.OrdinalIgnoreCase))
        {
            message = "Map path must point to a .yml file.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool TryGetConfiguredBaseMapPath(WeeklyModeSet set, out ResPath mapPath)
    {
        mapPath = default;
        if (string.IsNullOrWhiteSpace(set.BaseMapPath))
            return false;

        if (!TryNormalizeContentMapPath(set.BaseMapPath, out var resolved, out _))
            return false;

        mapPath = resolved;
        return true;
    }

    private static bool ContainsTraversal(ResPath path)
    {
        foreach (var segment in path.CanonPath.Split(ResPath.SeparatorStr, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
                return true;
        }

        return false;
    }

    private IEnumerable<string> ValidateWeeklyTechnologies(WeeklyModeSet set)
    {
        return ValidateWeeklyTechnologies(set, _store.LoadRecipesOrDefault(set.SetId));
    }

    private IEnumerable<string> ValidateWeeklyTechnologies(WeeklyModeSet set, WeeklyRecipesConfig recipes)
    {
        var weeklyRecipeIds = recipes.Recipes.Select(recipe => recipe.Id).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in set.WeeklyTechnologies)
        {
            if (!TryNormalizeTechnologyBranch(entry.Branch, out var normalizedBranch, out var branchError))
            {
                yield return $"technology '{entry.TechnologyId}' has invalid branch: {branchError}";
            }
            else if (!WeeklyTechnologyDisciplineIds.TryGetValue(normalizedBranch, out var disciplineId) ||
                     !_prototype.TryIndex<TechDisciplinePrototype>(disciplineId, out _))
            {
                yield return $"technology '{entry.TechnologyId}' branch '{entry.Branch}' has no matching research discipline";
            }

            if (!WeeklyModeStore.IsSafeId(entry.TechnologyId))
                yield return $"technologyId '{entry.TechnologyId}' is not a safe id";

            if (!seen.Add(entry.TechnologyId))
                yield return $"duplicate weekly technology '{entry.TechnologyId}'";

            if (entry.Cost < 0)
                yield return $"technology '{entry.TechnologyId}' cost may not be negative";

            if (entry.Tier is < 1 or > 3)
                yield return $"technology '{entry.TechnologyId}' tier must be 1, 2 or 3";

            foreach (var recipeId in entry.RecipeIds.Distinct(StringComparer.Ordinal))
            {
                if (!_prototype.TryIndex<LatheRecipePrototype>(recipeId, out _) &&
                    !weeklyRecipeIds.Contains(recipeId))
                {
                    yield return $"technology '{entry.TechnologyId}' references unknown recipe '{recipeId}'";
                }
            }
        }
    }

    private IEnumerable<string> ValidateWeeklyRecipes(WeeklyModeSet set, WeeklyRecipesConfig recipes)
    {
        if (recipes.SchemaVersion != WeeklyRecipesConfig.CurrentSchemaVersion)
            yield return $"recipes schemaVersion {recipes.SchemaVersion} is not compatible with {WeeklyRecipesConfig.CurrentSchemaVersion}";

        var technologyIds = set.WeeklyTechnologies
            .Select(technology => technology.TechnologyId)
            .ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipe in recipes.Recipes)
        {
            if (!WeeklyModeStore.IsSafeId(recipe.Id))
                yield return $"recipeId '{recipe.Id}' is not a safe id";

            if (!seen.Add(recipe.Id))
                yield return $"duplicate weekly recipe '{recipe.Id}'";

            if (_prototype.HasIndex<LatheRecipePrototype>(recipe.Id))
                yield return $"recipe '{recipe.Id}' conflicts with a global lathe recipe prototype";

            if (!_prototype.TryIndex<EntityPrototype>(recipe.ResultPrototype, out var resultPrototype))
            {
                yield return $"recipe '{recipe.Id}' references unknown result entity prototype '{recipe.ResultPrototype}'";
            }
            else if (resultPrototype.Abstract)
            {
                yield return $"recipe '{recipe.Id}' result entity prototype '{recipe.ResultPrototype}' is abstract";
            }

            if (recipe.ResultAmount < 1)
                yield return $"recipe '{recipe.Id}' resultAmount must be at least 1";

            if (!double.IsFinite(recipe.ProductionTimeSeconds) || recipe.ProductionTimeSeconds <= 0)
                yield return $"recipe '{recipe.Id}' productionTimeSeconds must be greater than zero";

            if (recipe.LatheTargets.Count == 0)
                yield return $"recipe '{recipe.Id}' must specify at least one lathe target";

            foreach (var target in recipe.LatheTargets)
            {
                if (!TryNormalizeLatheTarget(target, out _, out var targetError))
                    yield return $"recipe '{recipe.Id}' has invalid target '{target}': {targetError}";
            }

            if (recipe.Materials.Count == 0)
                yield return $"recipe '{recipe.Id}' must specify at least one material";

            foreach (var (materialId, amount) in recipe.Materials)
            {
                if (!_prototype.HasIndex<MaterialPrototype>(materialId))
                    yield return $"recipe '{recipe.Id}' references unknown material '{materialId}'";

                if (amount <= 0)
                    yield return $"recipe '{recipe.Id}' material '{materialId}' amount must be greater than zero";
            }

            foreach (var technologyId in recipe.TechnologyIds.Distinct(StringComparer.Ordinal))
            {
                if (!technologyIds.Contains(technologyId))
                    yield return $"recipe '{recipe.Id}' references unknown weekly technology '{technologyId}'";
            }
        }
    }

    private IEnumerable<string> ValidateWeeklyCargoProducts(WeeklyModeSet set)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in set.WeeklyCargoProducts)
        {
            if (!WeeklyModeStore.IsSafeId(entry.ProductId))
                yield return $"cargo productId '{entry.ProductId}' is not a safe id";

            if (!seen.Add(entry.ProductId))
                yield return $"duplicate weekly cargo product '{entry.ProductId}'";

            if (string.IsNullOrWhiteSpace(entry.Category))
                yield return $"cargo product '{entry.ProductId}' category may not be empty";

            if (entry.Cost < 0)
                yield return $"cargo product '{entry.ProductId}' cost may not be negative";

            if (entry.Amount < 1)
                yield return $"cargo product '{entry.ProductId}' amount must be at least 1";

            if (!_prototype.TryIndex<EntityPrototype>(entry.ItemPrototype, out _))
                yield return $"cargo product '{entry.ProductId}' references unknown item prototype '{entry.ItemPrototype}'";

            if (entry.Boxed && entry.Amount > WeeklyCargoBoxedCapacity)
                yield return $"cargo product '{entry.ProductId}' boxed amount exceeds default crate capacity ({WeeklyCargoBoxedCapacity})";
        }
    }

    private static string DescribeBaseMap(WeeklyModeSet set)
    {
        return string.IsNullOrWhiteSpace(set.BaseMapPath)
            ? set.BaseMapPrototype
            : $"{set.BaseMapPath} ({set.BaseMapPrototype})";
    }

    private void SendAutosaveOoc(string message)
    {
        _chatManager.ChatMessageToAll(ChatChannel.OOC, message, message, EntityUid.Invalid, false, true);
    }

    private void SendAccessDeniedNotice(ICommonSession session, WeeklyModeSet? set, bool force)
    {
        if (set == null || set.MinPlaytimeHours <= 0)
            return;

        if (!force &&
            _accessNoticeCooldowns.TryGetValue(session.UserId, out var nextAllowed) &&
            _timing.CurTime < nextAllowed)
        {
            return;
        }

        _accessNoticeCooldowns[session.UserId] = _timing.CurTime + TimeSpan.FromSeconds(30);
        _chatManager.DispatchServerMessage(session, BuildAccessDeniedNotice(set));
    }

    private static string BuildAccessDeniedNotice(WeeklyModeSet set)
    {
        var discord = string.IsNullOrWhiteSpace(set.DiscordChannel)
            ? "Discord-канале сервера"
            : set.DiscordChannel;

        return
            $"Для участия в этом мероприятии необходимо иметь минимум {set.MinPlaytimeHours} часов игрового времени именно на этом сервере.\n\n" +
            "Это ограничение используется для защиты мероприятия от организованных набегов и случайных нарушителей.\n\n" +
            "Вы пока не можете выбирать роли, но можете наблюдать за игрой в качестве призрака.\n\n" +
            $"Дополнительная информация о мероприятии находится в Discord-канале сервера: {discord}";
    }

    private static string FormatList(IEnumerable<string> values)
    {
        var ordered = values.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return ordered.Length == 0 ? "<none>" : string.Join(", ", ordered);
    }

    private static string FormatDictionary<TValue>(IReadOnlyDictionary<string, TValue> values)
    {
        if (values.Count == 0)
            return "<none>";

        return string.Join(", ", values
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={x.Value}"));
    }

    private static bool TryNormalizeAlias(string rawAlias, out string alias, out string message)
    {
        alias = rawAlias.Trim().Trim('"');

        if (alias.Length == 0)
        {
            message = "Alias may not be empty.";
            return false;
        }

        if (alias.Length > 64)
        {
            message = "Alias may not be longer than 64 characters.";
            return false;
        }

        foreach (var c in alias)
        {
            if (char.IsControl(c))
            {
                message = "Alias may not contain control characters.";
                return false;
            }

            if (c is '[' or ']')
            {
                message = "Alias may not contain markup brackets '[' or ']'.";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private static bool TryNormalizeTechnologyBranch(string rawBranch, out string branch, out string message)
    {
        branch = rawBranch.Trim().ToLowerInvariant();
        if (!WeeklyTechnologyBranches.Contains(branch))
        {
            message = $"Branch must be one of: {string.Join(", ", WeeklyTechnologyBranches.OrderBy(x => x, StringComparer.Ordinal))}.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays >= 1)
            return $"{age.TotalDays:F1}d";

        if (age.TotalHours >= 1)
            return $"{age.TotalHours:F1}h";

        if (age.TotalMinutes >= 1)
            return $"{age.TotalMinutes:F1}m";

        return $"{Math.Max(0, age.TotalSeconds):F0}s";
    }

    private void PruneAutosaves(WeeklyModeSet set)
    {
        var retain = Math.Max(1, set.RetainAutosaves);
        var autos = new List<WeeklySnapshotMetadata>();

        foreach (var snapshotId in set.Snapshots)
        {
            if (_store.TryLoadSnapshot(set.SetId, snapshotId, out var metadata) &&
                metadata.Kind == WeeklySnapshotKind.Auto)
            {
                autos.Add(metadata);
            }
        }

        foreach (var metadata in autos.OrderByDescending(x => x.CreatedAtUtc).Skip(retain))
        {
            if (metadata.SnapshotId == set.CurrentSnapshot)
                continue;

            _store.DeleteSnapshot(set.SetId, metadata.SnapshotId);
            set.Snapshots.Remove(metadata.SnapshotId);
        }
    }

    private bool IsActiveSet(string setId)
    {
        return _state.IsActive && _state.ActiveSetId == setId;
    }

    private bool EnsureEnabled(out string message)
    {
        if (_enabled)
        {
            message = string.Empty;
            return true;
        }

        message = "Weekly mode is disabled. Set weekly_mode.enabled to true first.";
        return false;
    }

    private bool TryEnterOperation(out string message)
    {
        if (_operationInProgress)
        {
            message = "A weekly mode operation is already in progress.";
            return false;
        }

        _operationInProgress = true;
        message = string.Empty;
        return true;
    }

    private void RestartForWeeklyMap()
    {
        _nextAutosaveAt = null;
        _originalSlots.Clear();
        _gameTicker.RestartRound();
    }

    private void RebuildStore(string root)
    {
        _store = new WeeklyModeStore(_resource, root);
    }

    private static void CopyState(WeeklyModeRuntimeState source, WeeklyModeRuntimeState target)
    {
        target.SchemaVersion = source.SchemaVersion;
        target.IsActive = source.IsActive;
        target.ActiveSetId = source.ActiveSetId;
        target.ActiveSnapshotId = source.ActiveSnapshotId;
        target.PendingSnapshotId = source.PendingSnapshotId;
        target.ActiveWeeklyMapId = source.ActiveWeeklyMapId;
        target.ActiveWeeklyMapEntity = source.ActiveWeeklyMapEntity;
        target.ActiveWeeklyGridIds = source.ActiveWeeklyGridIds?.ToList() ?? new List<int>();
        target.ActiveWeeklyMapPrototype = source.ActiveWeeklyMapPrototype;
        target.ActiveWeeklyMapName = source.ActiveWeeklyMapName;
        target.StartedAtUtc = source.StartedAtUtc;
        target.StartedBy = source.StartedBy;
    }

    private static string BuildSnapshotId(WeeklySnapshotKind kind)
    {
        var prefix = kind switch
        {
            WeeklySnapshotKind.Auto => "auto",
            WeeklySnapshotKind.Manual => "manual",
            WeeklySnapshotKind.Endshift => "endshift",
            WeeklySnapshotKind.RollbackPoint => "rollback-point",
            WeeklySnapshotKind.RollbackBackup => "rollback-backup",
            _ => "snapshot",
        };

        return $"{prefix}-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
    }
}
