using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderPrincessComponent : Component
{
    [DataField]
    public EntProtoId RemoteViewNextAction = "ActionTerrorSpiderPrincessRemoteViewNext";

    [DataField]
    public EntityUid? RemoteViewNextActionEntity;

    [DataField]
    public EntProtoId RemoteViewPreviousAction = "ActionTerrorSpiderPrincessRemoteViewPrevious";

    [DataField]
    public EntityUid? RemoteViewPreviousActionEntity;

    [DataField]
    public EntProtoId RemoteViewExitAction = "ActionTerrorSpiderPrincessRemoteViewExit";

    [DataField]
    public EntityUid? RemoteViewExitActionEntity;

    [DataField]
    public EntProtoId HiveSenseAction = "ActionTerrorSpiderPrincessHiveSense";

    [DataField]
    public EntityUid? HiveSenseActionEntity;

    [DataField]
    public EntProtoId ScreamAction = "ActionTerrorSpiderPrincessScream";

    [DataField]
    public EntityUid? ScreamActionEntity;

    [DataField]
    public EntProtoId UnweldVentAction = "ActionTerrorSpiderVentUnweld";

    [DataField]
    public EntityUid? UnweldVentActionEntity;

    [DataField]
    public EntProtoId LayEggRusarAction = "ActionTerrorSpiderPrincessLayEggRusar";

    [DataField]
    public EntityUid? LayEggRusarActionEntity;

    [DataField]
    public EntProtoId LayEggDronAction = "ActionTerrorSpiderPrincessLayEggDron";

    [DataField]
    public EntityUid? LayEggDronActionEntity;

    [DataField]
    public EntProtoId LayEggLurkerAction = "ActionTerrorSpiderPrincessLayEggLurker";

    [DataField]
    public EntityUid? LayEggLurkerActionEntity;

    [DataField]
    public EntProtoId LayEggHealerAction = "ActionTerrorSpiderPrincessLayEggHealer";

    [DataField]
    public EntityUid? LayEggHealerActionEntity;

    [DataField]
    public EntProtoId LayEggReaperAction = "ActionTerrorSpiderPrincessLayEggReaper";

    [DataField]
    public EntityUid? LayEggReaperActionEntity;

    [DataField]
    public EntProtoId LayEggWidowAction = "ActionTerrorSpiderPrincessLayEggWidow";

    [DataField]
    public EntityUid? LayEggWidowActionEntity;

    [DataField]
    public EntProtoId LayEggGuardianAction = "ActionTerrorSpiderPrincessLayEggGuardian";

    [DataField]
    public EntityUid? LayEggGuardianActionEntity;

    [DataField]
    public EntProtoId LayEggDestroyerAction = "ActionTerrorSpiderPrincessLayEggDestroyer";

    [DataField]
    public EntityUid? LayEggDestroyerActionEntity;

    [DataField]
    public float ScreamRange = 13f;

    [DataField]
    public float ScreamSlowDuration = 10f;

    [DataField]
    public float ScreamSlowMultiplier = 0.5f;

    [DataField]
    public float ScreamStaminaDamage = 30f;

    [DataField]
    public float ScreamEmpEnergyConsumption = 120000f;

    [DataField]
    public float ScreamRobotDisableSeconds = 12f;

    [DataField]
    public SoundSpecifier? ScreamSound = new SoundPathSpecifier("/Audio/Imperial/TerrorSpider/scream.ogg");

    [DataField]
    public EntProtoId RemoteViewImmobileStatusEffect = "TerrorSpiderPrincessRemoteViewImmobileStatusEffect";

    [DataField]
    public float RemoteViewImmobileRefresh = 0.5f;

    [DataField]
    public int MaxBroodOnMap = 20;

    [DataField]
    public int MaxEliteBroodOnMap = 3;

    [DataField]
    public float OrphanDamagePerTick = 6f;

    [DataField]
    public float OrphanDamageInterval = 2f;

    [ViewVariables]
    public int RemoteViewIndex = -1;
}
