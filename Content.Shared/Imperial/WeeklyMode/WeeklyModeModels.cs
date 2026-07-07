

using System.Text.Json.Serialization;
using Robust.Shared.GameObjects;

namespace Content.Shared.WeeklyMode;

public enum WeeklySnapshotKind
{
    Auto,
    Manual,
    Endshift,
    RollbackPoint,
    RollbackBackup,
}

public sealed class WeeklyModeSet
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string SetId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BaseMapPrototype { get; set; } = string.Empty;
    public string BaseMapPath { get; set; } = string.Empty;
    public int AutosaveMinutes { get; set; } = 30;
    public int AutosaveWarningMinutes { get; set; } = 2;
    public int RetainAutosaves { get; set; } = 8;
    public string? CurrentSnapshot { get; set; }
    public List<string> Snapshots { get; set; } = new();
    public List<string> DefaultDisabledJobs { get; set; } = new();
    public Dictionary<string, string> DefaultRoleAliases { get; set; } = new();
    public Dictionary<string, int> DefaultRoleLimits { get; set; } = new();
    public bool PersistAutonomousMobs { get; set; } = true;
    public bool PersistPlayerControlledBorgs { get; set; } = true;
    public List<string> ExcludedMobPrototypes { get; set; } = new()
    {
        "MobMouse",
        "MobMouseDead",
        "MobMouseAdmeme",
        "MobMouse1",
        "MobMouse2",
        "MobMouseCancer",
    };
    public int MinPlaytimeHours { get; set; }
    public string DiscordChannel { get; set; } = string.Empty;
    public bool RandomGameRulesEnabled { get; set; }
    public List<WeeklyTechnologyEntry> WeeklyTechnologies { get; set; } = new();
    public List<WeeklyCargoProductEntry> WeeklyCargoProducts { get; set; } = new();
    [JsonPropertyName("forcedRoleAssignments")]
    public List<WeeklyForcedRoleAssignment> ForcedRoleAssignments { get; set; } = new();
    public Dictionary<string, string> CampaignState { get; set; } = new();
}

public sealed class WeeklyForcedRoleAssignment
{
    [JsonPropertyName("playerNetUserId")]
    public string PlayerNetUserId { get; set; } = string.Empty;

    [JsonPropertyName("lastKnownCKey")]
    public string LastKnownCKey { get; set; } = string.Empty;

    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("bypassPlaytime")]
    public bool BypassPlaytime { get; set; }
}

public sealed class WeeklyTechnologyEntry
{
    public string TechnologyId { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public int Cost { get; set; }
    public int Tier { get; set; } = 1;
    public List<string> RecipeIds { get; set; } = new();
}

public sealed class WeeklyCargoProductEntry
{
    public string ProductId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Cost { get; set; }
    public bool Boxed { get; set; }
    public int Amount { get; set; } = 1;
    public string ItemPrototype { get; set; } = string.Empty;
}

public sealed class WeeklyRecipesConfig
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("recipes")]
    public List<WeeklyRecipeDefinition> Recipes { get; set; } = new();
}

public sealed class WeeklyRecipeDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("resultPrototype")]
    public string ResultPrototype { get; set; } = string.Empty;

    [JsonPropertyName("resultAmount")]
    public int ResultAmount { get; set; } = 1;

    [JsonPropertyName("productionTimeSeconds")]
    public double ProductionTimeSeconds { get; set; } = 5;

    [JsonPropertyName("applyMaterialDiscount")]
    public bool ApplyMaterialDiscount { get; set; } = true;

    [JsonPropertyName("latheTargets")]
    public List<string> LatheTargets { get; set; } = new();

    [JsonPropertyName("materials")]
    public Dictionary<string, int> Materials { get; set; } = new();

    [JsonPropertyName("technologyIds")]
    public List<string> TechnologyIds { get; set; } = new();
}

public sealed class WeeklyRecipesChangedEvent : EntityEventArgs;

public sealed class WeeklyModeRuntimeState
{
    public int SchemaVersion { get; set; } = 1;
    public bool IsActive { get; set; }
    public string? ActiveSetId { get; set; }
    public string? ActiveSnapshotId { get; set; }
    public string? PendingSnapshotId { get; set; }
    public int? ActiveWeeklyMapId { get; set; }
    public int? ActiveWeeklyMapEntity { get; set; }
    public List<int> ActiveWeeklyGridIds { get; set; } = new();
    public string? ActiveWeeklyMapPrototype { get; set; }
    public string? ActiveWeeklyMapName { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public string? StartedBy { get; set; }
}

public sealed class WeeklySnapshotMetadata
{
    public int SchemaVersion { get; set; } = WeeklyModeSet.CurrentSchemaVersion;
    public string SnapshotId { get; set; } = string.Empty;
    public string SetId { get; set; } = string.Empty;


    public WeeklySnapshotKind Kind { get; set; }

    public string BaseMapPrototype { get; set; } = string.Empty;
    public string BaseMapPath { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string ContentVersion { get; set; } = string.Empty;
    public string EngineVersion { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public long BundleSizeBytes { get; set; }
    public bool CompatibleWithBuild { get; set; } = true;
    public int? SavedMapId { get; set; }
    public string SavedMapName { get; set; } = string.Empty;
    public List<int> SavedGridIds { get; set; } = new();
    public int EntityCount { get; set; }
}

public sealed class WeeklyRoleOverrides
{
    public List<string> DisabledJobs { get; set; } = new();
    public Dictionary<string, string> RoleAliases { get; set; } = new();
    public Dictionary<string, int> RoleLimits { get; set; } = new();
}

public sealed class WeeklyContainerPatch
{
    public int SchemaVersion { get; set; } = WeeklyModeSet.CurrentSchemaVersion;
    public List<WeeklyContainerEntry> Entries { get; set; } = new();
    public int SkippedExcluded { get; set; }
    public int SkippedUnserialized { get; set; }
}

public sealed class WeeklyContainerEntry
{
    public int OwnerYamlUid { get; set; }
    public string OwnerPrototype { get; set; } = string.Empty;
    public string ContainerId { get; set; } = string.Empty;
    public int ChildYamlUid { get; set; }
    public string ChildPrototype { get; set; } = string.Empty;
    public int Index { get; set; }
    public int OwnerDepth { get; set; }
}

public sealed class WeeklySnapshotIntegrity
{
    public int SchemaVersion { get; set; } = WeeklyModeSet.CurrentSchemaVersion;
    public string SnapshotId { get; set; } = string.Empty;
    public string SetId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public Dictionary<string, WeeklySnapshotFileIntegrity> Files { get; set; } = new();
}

public sealed class WeeklySnapshotFileIntegrity
{
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}
