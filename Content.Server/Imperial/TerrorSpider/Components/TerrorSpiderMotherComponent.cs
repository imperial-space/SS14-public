using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderMotherComponent : Component
{
    [DataField]
    public EntProtoId PulseAction = "ActionTerrorSpiderMotherPulse";

    [DataField]
    public EntityUid? PulseActionEntity;

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
    public EntProtoId LayJellyAction = "ActionTerrorSpiderMotherLayJelly";

    [DataField]
    public EntityUid? LayJellyActionEntity;

    [DataField]
    public float AuraHalfRange = 7.5f;

    [DataField]
    public float AuraHealAmount = 3f;

    [DataField]
    public float AuraDamageAmount = 3f;

    [DataField]
    public float AuraInterval = 2f;

    [DataField]
    public float TouchHealAmount = 2f;

    [DataField]
    public float PulseHealAmount = 30f;

    [DataField]
    public float PulseRange = 13f;

    [DataField]
    public EntProtoId JellyPrototype = "TerrorSpiderMotherJelly";

    [DataField]
    public EntProtoId RemoteViewImmobileStatusEffect = "TerrorSpiderMotherRemoteViewImmobileStatusEffect";

    [DataField]
    public float RemoteViewImmobileRefresh = 0.5f;

    [ViewVariables]
    public int RemoteViewIndex = -1;

    [ViewVariables]
    public TimeSpan NextAuraTick = TimeSpan.Zero;
}
