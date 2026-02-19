using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.SCP.SCP173.Components;

[RegisterComponent]
public sealed partial class SCP173LightFlickerComponent : Component
{
    [DataField("action")]
    public EntProtoId ActionProto = "ActionSCP173LightFlicker";

    [DataField("radius")]
    public float Radius = 9f;

    [DataField("duration")]
    public TimeSpan Duration = TimeSpan.FromSeconds(8);

    [DataField("toggleInterval")]
    public TimeSpan ToggleInterval = TimeSpan.FromSeconds(0.25f);

    [DataField("activationSound")]
    public SoundSpecifier? ActivationSound = new SoundPathSpecifier("/Audio/Machines/lightswitch.ogg");

    [ViewVariables]
    public EntityUid? ActionEntity;

    [ViewVariables]
    public bool IsActive;

    [ViewVariables]
    public bool LightsOff;

    [ViewVariables]
    public TimeSpan EndTime;

    [ViewVariables]
    public TimeSpan NextToggle;

    [ViewVariables]
    public Dictionary<EntityUid, bool> CapturedLightStates = new();
}