using Robust.Shared.Audio;

namespace Content.Server.Imperial.SCP.SCP096.Components;

[RegisterComponent]
public sealed partial class SCP096RageOnLookComponent : Component
{
    [DataField("observeRadius")]
    public float ObserveRadius = 9f;

    [DataField("minLookDot")]
    public float MinLookDot = 0.42f;

    [DataField("requireUnobstructed")]
    public bool RequireUnobstructed = true;

    [DataField("enragedWalkModifier")]
    public float EnragedWalkModifier = 1.7f;

    [DataField("enragedSprintModifier")]
    public float EnragedSprintModifier = 1.7f;

    [DataField("enragedAttackRate")]
    public float EnragedAttackRate = 1.7f;

    [DataField("calmAttackRate")]
    public float CalmAttackRate = 1.0f;

    [DataField("rageWindup")]
    public TimeSpan RageWindup = TimeSpan.FromSeconds(10);

    [DataField("rageDuration")]
    public TimeSpan RageDuration = TimeSpan.FromSeconds(120);

    [DataField("rageWindupPopup")]
    public LocId RageWindupPopup = "scp096-rage-windup-popup";

    [DataField("ragePopup")]
    public LocId RagePopup = "scp096-rage-popup";

    [DataField("rageCalmPopup")]
    public LocId RageCalmPopup = "scp096-rage-calm-popup";

    [DataField("rageSound")]
    public SoundSpecifier RageSound = new SoundPathSpecifier("/Audio/Voice/Human/malescream_1.ogg");

    [DataField("cryingSound")]
    public SoundSpecifier CryingSound = new SoundPathSpecifier("/Audio/Voice/Human/cry_male_1.ogg");

    [DataField("rageLoopSound")]
    public SoundSpecifier RageLoopSound = new SoundPathSpecifier("/Audio/Ambience/Objects/anomaly_generator_ambi.ogg");

    [ViewVariables]
    public bool IsEnraged;

    [ViewVariables]
    public bool IsRageWindup;

    [ViewVariables]
    public TimeSpan RageWindupEndTime;

    [ViewVariables]
    public TimeSpan RageEndTime;

    [ViewVariables]
    public bool UsingRageLoopSound;

    [ViewVariables]
    public float? OriginalAttackRate;

    [ViewVariables]
    public HashSet<EntityUid> RageTargets = new();
}
