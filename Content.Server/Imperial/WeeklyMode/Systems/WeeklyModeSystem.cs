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

public sealed partial class WeeklyModeSystem : EntitySystem
{

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
        RebuildStore(_cfg.GetCVar(ICCVars.WeeklyModeDataRoot));

        _enabled = _cfg.GetCVar(ICCVars.WeeklyModeEnabled);
        Subs.CVar(_cfg, ICCVars.WeeklyModeEnabled, value => _enabled = value, true);
        Subs.CVar(_cfg, ICCVars.WeeklyModeDataRoot, RebuildStore, true);

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
                Loc.GetString("weekly-mode-autosave-warning", ("minutes", set.AutosaveWarningMinutes)));
            _autosaveWarningIssued = true;
        }

        if (_timing.CurTime < _nextAutosaveAt.Value)
            return;

        SendAutosaveOoc(Loc.GetString("weekly-mode-autosave-start"));

        if (TrySaveSnapshot(set.SetId, WeeklySnapshotKind.Auto, "scheduled autosave", "server", out var message))
        {
            SendAutosaveOoc(Loc.GetString("weekly-mode-autosave-complete"));
        }
        else
        {
            _sawmill.Warning($"Weekly autosave failed: {message}");
            SendAutosaveOoc(Loc.GetString("weekly-mode-autosave-failed"));
        }

        _nextAutosaveAt = _timing.CurTime + TimeSpan.FromMinutes(Math.Max(1, set.AutosaveMinutes));
        _autosaveWarningIssued = false;
    }
}
