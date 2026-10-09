using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Blob.Components;

/// <summary>
/// Оверманд блоба (mob/eye/blob из SS13). Значения по умолчанию — из blob_defines.dm.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlobOvermindComponent : Component
{
    #region Константы SS13

    /// <summary>OVERMIND_STARTING_POINTS.</summary>
    [DataField]
    public float StartingPoints = 60f;

    /// <summary>OVERMIND_MAX_POINTS_DEFAULT.</summary>
    [DataField, AutoNetworkedField]
    public float MaxPoints = 100f;

    /// <summary>BLOB_BASE_POINT_RATE.</summary>
    [DataField]
    public float BasePointRate = 2f;

    /// <summary>BLOB_CORE_HP_REGEN.</summary>
    [DataField]
    public float BaseCoreRegen = 2f;

    /// <summary>BLOB_EXPAND_COST.</summary>
    [DataField]
    public float ExpandCost = 4f;

    /// <summary>BLOB_ATTACK_REFUND.</summary>
    [DataField]
    public float AttackRefund = 2f;

    [DataField] public float StrongCost = 15f;
    [DataField] public float ReflectorCost = 15f;
    [DataField] public float ResourceCost = 40f;
    [DataField] public float FactoryCost = 60f;
    [DataField] public float NodeCost = 50f;
    [DataField] public float RelocateCost = 80f;
    [DataField] public float RerollCost = 40f;
    [DataField] public float BlobbernautCost = 40f;

    [DataField] public int ResourceMinDistance = 4;
    [DataField] public int FactoryMinDistance = 7;
    [DataField] public int NodeMinDistance = 5;

    /// <summary>BLOB_NODE_PULSE_RANGE / BLOB_CORE_PULSE_RANGE для nodes_required.</summary>
    [DataField] public int NodePulseRange = 3;
    [DataField] public int CorePulseRange = 4;

    /// <summary>BLOB_POWER_REROLL_FREE_TIME.</summary>
    [DataField]
    public TimeSpan RerollFreeTime = TimeSpan.FromMinutes(4);

    /// <summary>BLOB_POWER_REROLL_CHOICES.</summary>
    [DataField]
    public int RerollChoices = 6;

    /// <summary>OVERMIND_STARTING_MIN_PLACE_TIME.</summary>
    [DataField]
    public TimeSpan ManualPlaceDelay = TimeSpan.FromMinutes(1);

    /// <summary>OVERMIND_STARTING_AUTO_PLACE_TIME.</summary>
    [DataField]
    public TimeSpan AutoPlaceDelay = TimeSpan.FromMinutes(6);

    /// <summary>OVERMIND_WIN_CONDITION_AMOUNT.</summary>
    [DataField, AutoNetworkedField]
    public int WinCount = 400;

    /// <summary>OVERMIND_ANNOUNCEMENT_MIN_SIZE.</summary>
    [DataField]
    public int AnnouncementSize = 75;

    /// <summary>OVERMIND_ANNOUNCEMENT_MAX_TIME.</summary>
    [DataField]
    public TimeSpan AnnouncementDelay = TimeSpan.FromMinutes(10);

    /// <summary>Задержка между «критической массой» и победой.</summary>
    [DataField]
    public TimeSpan VictoryDelay = TimeSpan.FromSeconds(45);

    /// <summary>OVERMIND_MAX_CAMERA_STRAY: 3x3.</summary>
    [DataField]
    public int MaxCameraStray = 1;

    /// <summary>CLICK_CD_MELEE / CLICK_CD_RAPID.</summary>
    [DataField] public TimeSpan AttackCooldown = TimeSpan.FromSeconds(0.8);
    [DataField] public TimeSpan ExpandCooldown = TimeSpan.FromSeconds(0.2);

    /// <summary>end_round_on_victory.</summary>
    [DataField]
    public bool EndRoundOnVictory = true;

    #endregion

    #region Прототипы

    [DataField] public EntProtoId CorePrototype = "BlobCore";
    [DataField] public EntProtoId NodePrototype = "BlobNode";
    [DataField] public EntProtoId FactoryPrototype = "BlobFactory";
    [DataField] public EntProtoId ResourcePrototype = "BlobResource";
    [DataField] public EntProtoId StrongPrototype = "BlobStrong";
    [DataField] public EntProtoId ReflectivePrototype = "BlobReflective";
    [DataField] public EntProtoId BlobbernautSpawnerPrototype = "SpawnPointGhostBlobbernaut";

    [DataField]
    public List<EntProtoId> ActionPrototypes = new()
    {
        "ActionBlobJumpToCore",
        "ActionBlobJumpToNode",
        "ActionBlobCreateResource",
        "ActionBlobCreateNode",
        "ActionBlobCreateFactory",
        "ActionBlobCreateBlobbernaut",
        "ActionBlobReadaptStrain",
        "ActionBlobRelocateCore",
    };

    [DataField]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Imperial/blob/blobalert.ogg");

    [DataField]
    public SoundSpecifier OutbreakSound = new SoundPathSpecifier("/Audio/Imperial/blob/outbreak5.ogg");

    [DataField]
    public SoundSpecifier NukeAlarmSound = new SoundPathSpecifier("/Audio/Imperial/blob/nuke_alarm.ogg");

    [DataField]
    public SoundSpecifier SplatSound = new SoundPathSpecifier("/Audio/Imperial/blob/splat.ogg");

    #endregion

    #region Состояние (передаётся клиенту для HUD)

    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float Points;

    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public ProtoId<BlobStrainPrototype> Strain = "BlobReactiveSpines";

    /// <summary>Здоровье ядра в процентах; -1 — ядра нет.</summary>
    [ViewVariables, AutoNetworkedField]
    public int CoreHealth = -1;

    /// <summary>blobs_legit.len.</summary>
    [ViewVariables, AutoNetworkedField]
    public int BlobCount;

    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public int FreeRerolls = 1;

    [ViewVariables, AutoNetworkedField]
    public bool Placed;

    /// <summary>manualplace_min_time; TimeSpan.Zero — уже можно.</summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan ManualPlaceTime;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan AutoPlaceTime;

    [ViewVariables, AutoNetworkedField]
    public bool VictoryInProgress;

    #endregion

    #region Серверное состояние

    [ViewVariables] public EntityUid? Core;
    [ViewVariables] public HashSet<EntityUid> AllBlobs = new();
    [ViewVariables] public HashSet<EntityUid> BlobsLegit = new();
    [ViewVariables] public HashSet<EntityUid> Nodes = new();
    [ViewVariables] public HashSet<EntityUid> Factories = new();
    [ViewVariables] public HashSet<EntityUid> Resources = new();
    [ViewVariables] public HashSet<EntityUid> Mobs = new();
    [ViewVariables] public List<EntityUid> Actions = new();

    /// <summary>max_count: наибольший размер за раунд.</summary>
    [ViewVariables] public int MaxCount;

    [ViewVariables] public TimeSpan LastReroll;
    [ViewVariables] public TimeSpan LastAttack;
    [ViewVariables] public TimeSpan? AnnouncementTime;
    [ViewVariables] public bool HasAnnounced;
    [ViewVariables] public bool NodesRequired = true;
    [ViewVariables] public List<string>? StrainChoices;
    [ViewVariables] public TimeSpan? VictoryTime;
    [ViewVariables] public bool Victorious;
    [ViewVariables] public bool ManualPlaceNotified;
    [ViewVariables] public EntityCoordinates? LastValidPosition;
    [ViewVariables] public TimeSpan NextProcess;

    /// <summary>Правило, создавшее блоба.</summary>
    [ViewVariables] public EntityUid? Rule;

    #endregion
}
