using System;
using Robust.Shared.Prototypes;
using Content.Shared.Maps;

namespace Content.Shared.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobOvermindComponent : Component
{
    [DataField]
    public EntProtoId AttackActionPrototype = "ActionBlobAttack";

    [DataField]
    public EntProtoId ConsumeActionPrototype = "ActionBlobConsumeTile";

    [DataField]
    public EntProtoId PlaceTileActionPrototype = "ActionBlobPlaceTile";

    [DataField]
    public EntProtoId PlaceShieldTileActionPrototype = "ActionBlobPlaceShieldTile";

    [DataField]
    public EntProtoId PlaceNodeActionPrototype = "ActionBlobPlaceNode";

    [DataField]
    public EntProtoId PlaceFactoryActionPrototype = "ActionBlobPlaceFactory";

    [DataField]
    public EntProtoId PlaceResourceActionPrototype = "ActionBlobPlaceResource";

    [DataField]
    public EntProtoId PlaceStorageActionPrototype = "ActionBlobPlaceStorage";

    [DataField]
    public EntProtoId PlaceLauncherActionPrototype = "ActionBlobPlaceLauncher";

    [DataField]
    public EntProtoId PlaceCoolingActionPrototype = "ActionBlobPlaceCooling";

    [DataField]
    public EntProtoId SpawnBlobbernautActionPrototype = "ActionBlobSpawnBlobbernaut";

    [DataField]
    public EntProtoId RallyMinionsActionPrototype = "ActionBlobRallyMinions";

    [DataField]
    public EntProtoId UpgradeGenerationActionPrototype = "ActionBlobUpgradeGeneration";

    [DataField]
    public EntProtoId UpgradeAttackActionPrototype = "ActionBlobUpgradeAttack";

    [DataField]
    public EntProtoId UpgradeCapacityActionPrototype = "ActionBlobUpgradeCapacity";

    [DataField]
    public EntProtoId SplitConsciousnessActionPrototype = "ActionBlobSplitConsciousness";

    [DataField]
    public EntProtoId ShowStatusActionPrototype = "ActionBlobShowStatus";

    [DataField]
    public EntProtoId ChangeChemicalActionPrototype = "ActionBlobChangeChemical";

    [DataField]
    public int ChemicalChangeCost = 20;

    [DataField]
    public float BuildRange = 3f;

    [DataField]
    public float NodeBuildRange = 5f;

    [DataField]
    public float SpecialStructureRange = 3f;

    [DataField]
    public float NodeMinDistance = 5f;

    [DataField]
    public float FactoryMinDistance = 7f;

    [DataField]
    public float ResourceMinDistance = 4f;

    [DataField]
    public float StorageMinDistance = 4f;

    [DataField]
    public float LauncherMinDistance = 6f;

    [DataField]
    public float CoolingMinDistance = 5f;

    [DataField]
    public int StartingResources = 10;

    [DataField]
    public int MaxResources = 100;

    [DataField]
    public int TileCost = 5;

    [DataField]
    public float SpaceTileCostMultiplier = 2f;

    [DataField]
    public float DenseTileCostMultiplier = 3f;

    [DataField]
    public int DenseObstacleChewDamage = 60;

    [DataField]
    public int StructureAttackDamage = 60;

    [DataField]
    public int AttackCost = 1;

    [DataField]
    public float ActiveAttackDelay = 0.5f;

    [DataField]
    public float ActiveTileActionDelay = 0.5f;

    [DataField]
    public float ConsumeRefundRatio = 0.5f;

    [DataField]
    public int AttackBaseDamage = 12;

    [DataField]
    public int AttackBonusDamagePerSource = 2;

    [DataField]
    public int AttackChemicalDamage = 5;

    [DataField]
    public float AttackChemicalDuration = 4f;

    [DataField]
    public float SoriumThrowDistance = 2.25f;

    [DataField]
    public int ShieldTileCost = 5;

    [DataField]
    public int ReflectiveTileCost = 5;

    [DataField]
    public int NodeCost = 60;

    [DataField]
    public int FactoryCost = 60;

    [DataField]
    public int ResourceCost = 40;

    [DataField]
    public int StorageCost = 40;

    [DataField]
    public int LauncherCost = 7;

    [DataField]
    public int CoolingCost = 5;

    [DataField]
    public int BlobbernautCost = 60;

    [DataField]
    public int SplitConsciousnessCost = 100;

    [DataField]
    public int MaxCoreCount = 2;

    [DataField]
    public float ResourceTickInterval = 1f;

    [DataField]
    public float AutoSpreadInterval = 8f;

    [DataField]
    public int PassiveIncome = 1;

    [DataField]
    public int ResourceStructureIncome = 1;

    [DataField]
    public float ResourceStructureTickInterval = 2.7f;

    [DataField]
    public int StorageMaxResourceBonus = 50;

    [DataField]
    public int EvolutionThresholdStart = 25;

    [DataField]
    public int EvolutionThresholdStep = 25;

    [DataField]
    public int MaxUpgradeLevel = 3;

    [DataField]
    public int StorageCapacityLevelRequirement = 1;

    [DataField]
    public int LauncherAttackLevelRequirement = 1;

    [DataField]
    public int CoolingGenerationLevelRequirement = 1;

    [DataField]
    public int BlobbernautAttackLevelRequirement = 2;

    [DataField]
    public int SplitCapacityLevelRequirement = 2;

    [DataField]
    public int UpgradeBaseCost = 1;

    [DataField]
    public int UpgradeCostStep = 1;

    [DataField]
    public int GenerationUpgradeBonus = 1;

    [DataField]
    public float GenerationTickIntervalReduction = 0.5f;

    [DataField]
    public int AttackUpgradeBonus = 3;

    [DataField]
    public int AttackChemicalUpgradeBonus = 1;

    [DataField]
    public int CapacityUpgradeBonus = 20;

    [DataField]
    public float CapacityConsumeRefundBonus = 0.1f;

    [ViewVariables]
    public int Resources;

    [ViewVariables]
    public float ResourceAccumulator;

    [ViewVariables]
    public float ResourceStructureAccumulator;

    [ViewVariables]
    public int EvolutionPoints;

    [ViewVariables]
    public int NextEvolutionThreshold;

    [ViewVariables]
    public int GenerationUpgradeLevel;

    [ViewVariables]
    public int AttackUpgradeLevel;

    [ViewVariables]
    public int CapacityUpgradeLevel;

    [ViewVariables]
    public BlobChemicalType Chemical = BlobChemicalType.Sorium;

    [ViewVariables]
    public EntityUid? BlobId;

    [ViewVariables]
    public EntityUid? AttackAction;

    [ViewVariables]
    public EntityUid? ConsumeAction;

    [ViewVariables]
    public EntityUid? PlaceTileAction;

    [ViewVariables]
    public EntityUid? PlaceShieldTileAction;

    [ViewVariables]
    public EntityUid? PlaceNodeAction;

    [ViewVariables]
    public EntityUid? PlaceFactoryAction;

    [ViewVariables]
    public EntityUid? PlaceResourceAction;

    [ViewVariables]
    public EntityUid? PlaceStorageAction;

    [ViewVariables]
    public EntityUid? PlaceLauncherAction;

    [ViewVariables]
    public EntityUid? PlaceCoolingAction;

    [ViewVariables]
    public EntityUid? SpawnBlobbernautAction;

    [ViewVariables]
    public EntityUid? RallyMinionsAction;

    [ViewVariables]
    public EntityUid? UpgradeGenerationAction;

    [ViewVariables]
    public EntityUid? UpgradeAttackAction;

    [ViewVariables]
    public EntityUid? UpgradeCapacityAction;

    [ViewVariables]
    public EntityUid? SplitConsciousnessAction;

    [ViewVariables]
    public EntityUid? ShowStatusAction;

    [ViewVariables]
    public EntityUid? ChangeChemicalAction;

    [ViewVariables]
    public float AutoSpreadAccumulator;

    [ViewVariables]
    public TimeSpan NextAttackTime;

    [ViewVariables]
    public TimeSpan NextTileActionTime;

    [ViewVariables]
    public EntityUid? InteractionController;
}