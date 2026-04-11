using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderQueenComponent : Component
{
    [DataField]
    public EntProtoId CreateHiveAction = "ActionTerrorSpiderQueenCreateHive";

    [DataField]
    public EntityUid? CreateHiveActionEntity;

    [DataField]
    public EntProtoId ScreamAction = "ActionTerrorSpiderQueenScream";

    [DataField]
    public EntityUid? ScreamActionEntity;

    [DataField]
    public EntProtoId RemoteViewNextAction = "ActionTerrorSpiderMotherRemoteViewNext";

    [DataField]
    public EntityUid? RemoteViewNextActionEntity;

    [DataField]
    public EntProtoId RemoteViewPreviousAction = "ActionTerrorSpiderMotherRemoteViewPrevious";

    [DataField]
    public EntityUid? RemoteViewPreviousActionEntity;

    [DataField]
    public EntProtoId RemoteViewExitAction = "ActionTerrorSpiderMotherRemoteViewExit";

    [DataField]
    public EntityUid? RemoteViewExitActionEntity;

    [DataField]
    public EntProtoId HiveCountAction = "ActionTerrorSpiderQueenHiveCount";

    [DataField]
    public EntityUid? HiveCountActionEntity;

    [DataField]
    public EntProtoId LayEggRusarAction = "ActionTerrorSpiderQueenLayEggRusar";

    [DataField]
    public EntityUid? LayEggRusarActionEntity;

    [DataField]
    public EntProtoId LayEggDronAction = "ActionTerrorSpiderQueenLayEggDron";

    [DataField]
    public EntityUid? LayEggDronActionEntity;

    [DataField]
    public EntProtoId LayEggLurkerAction = "ActionTerrorSpiderQueenLayEggLurker";

    [DataField]
    public EntityUid? LayEggLurkerActionEntity;

    [DataField]
    public EntProtoId LayEggHealerAction = "ActionTerrorSpiderQueenLayEggHealer";

    [DataField]
    public EntityUid? LayEggHealerActionEntity;

    [DataField]
    public EntProtoId LayEggReaperAction = "ActionTerrorSpiderQueenLayEggReaper";

    [DataField]
    public EntityUid? LayEggReaperActionEntity;

    [DataField]
    public EntProtoId LayEggWidowAction = "ActionTerrorSpiderQueenLayEggWidow";

    [DataField]
    public EntityUid? LayEggWidowActionEntity;

    [DataField]
    public EntProtoId LayEggGuardianAction = "ActionTerrorSpiderQueenLayEggGuardian";

    [DataField]
    public EntityUid? LayEggGuardianActionEntity;

    [DataField]
    public EntProtoId LayEggDestroyerAction = "ActionTerrorSpiderQueenLayEggDestroyer";

    [DataField]
    public EntityUid? LayEggDestroyerActionEntity;

    [DataField]
    public EntProtoId LayEggPrinceAction = "ActionTerrorSpiderQueenLayEggPrince";

    [DataField]
    public EntityUid? LayEggPrinceActionEntity;

    [DataField]
    public EntProtoId LayEggPrincessAction = "ActionTerrorSpiderQueenLayEggPrincess";

    [DataField]
    public EntityUid? LayEggPrincessActionEntity;

    [DataField]
    public EntProtoId LayEggMotherAction = "ActionTerrorSpiderQueenLayEggMother";

    [DataField]
    public EntityUid? LayEggMotherActionEntity;

    [DataField]
    public bool HiveCreated;

    [DataField]
    public float HiveSpeedMultiplier = 0.5f;

    [DataField]
    public float HiveStructuralDamage = 400f;

    [DataField]
    public EntProtoId HiveSlowStatusEffect = "TerrorSpiderQueenHiveSlowStatusEffect";

    [DataField]
    public float HiveSlowRefresh = 0.5f;

    [DataField]
    public EntProtoId RemoteViewImmobileStatusEffect = "TerrorSpiderMotherRemoteViewImmobileStatusEffect";

    [DataField]
    public float RemoteViewImmobileRefresh = 0.5f;

    [DataField]
    public float ScreamRange = 8.5f;

    [DataField]
    public float ScreamSlowDuration = 14f;

    [DataField]
    public float ScreamSlowMultiplier = 0.5f;

    [DataField]
    public float ScreamStaminaDamage = 50f;

    [DataField]
    public float ScreamEmpEnergyConsumption = 120000f;

    [DataField]
    public float ScreamRobotDisableSeconds = 16f;

    [DataField]
    public float LightBreakHalfRange = 8.5f;

    [DataField]
    public float RoyalEggCooldownSeconds = 1500f;

    [ViewVariables]
    public Dictionary<string, TimeSpan> SharedEggCooldownEnds = new();

    [DataField]
    public float OrphanDamagePerTick = 6f;

    [DataField]
    public float OrphanDamageInterval = 2f;

    [DataField]
    public SoundSpecifier? ScreamSound = new SoundPathSpecifier("/Audio/Imperial/TerrorSpider/scream.ogg");

    [ViewVariables]
    public int RemoteViewIndex = -1;

    [ViewVariables]
    public Dictionary<string, TimeSpan> RoyalCooldownEnds = new();
}
