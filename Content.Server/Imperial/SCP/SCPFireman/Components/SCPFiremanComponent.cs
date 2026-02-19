using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.SCP.SCPFireman.Components;

[RegisterComponent]
public sealed partial class SCPFiremanComponent : Component
{
    [DataField("maxFirePoints")]
    public float MaxFirePoints = 100f;

    [DataField("firePoints")]
    public float FirePoints = 100f;

    [DataField("passiveRegenPerSecond")]
    public float PassiveRegenPerSecond = 1f;

    [DataField("secondModeDrainPerSecond")]
    public float SecondModeDrainPerSecond = 1.5f;

    [DataField("maxOwnedFires")]
    public int MaxOwnedFires = 80;

    [DataField("trueFlameDuration")]
    public TimeSpan TrueFlameDuration = TimeSpan.FromSeconds(25);

    [DataField("igniteAction")]
    public EntProtoId IgniteAction = "ActionSCPFiremanIgnite";

    [DataField("fireballAction")]
    public EntProtoId FireballAction = "ActionSCPFiremanFireball";

    [DataField("whirlAction")]
    public EntProtoId WhirlAction = "ActionSCPFiremanWhirl";

    [DataField("meltAction")]
    public EntProtoId MeltAction = "ActionSCPFiremanMelt";

    [DataField("trueFlameAction")]
    public EntProtoId TrueFlameAction = "ActionSCPFiremanTrueFlame";

    [DataField("strikeAction")]
    public EntProtoId StrikeAction = "ActionSCPFiremanStrike";

    [DataField("secondModeAction")]
    public EntProtoId SecondModeAction = "ActionSCPFiremanSecondMode";

    [DataField]
    public EntityUid? IgniteActionEntity;

    [DataField]
    public EntityUid? FireballActionEntity;

    [DataField]
    public EntityUid? WhirlActionEntity;

    [DataField]
    public EntityUid? MeltActionEntity;

    [DataField]
    public EntityUid? TrueFlameActionEntity;

    [DataField]
    public EntityUid? StrikeActionEntity;

    [DataField]
    public EntityUid? SecondModeActionEntity;

    [ViewVariables]
    public TimeSpan NextPassiveTick;

    [ViewVariables]
    public TimeSpan NextSecondModeDrainTick;

    [ViewVariables]
    public bool SecondModeEnabled;

    [ViewVariables]
    public bool TrueFlameActive;

    [ViewVariables]
    public TimeSpan TrueFlameEnd;
}
