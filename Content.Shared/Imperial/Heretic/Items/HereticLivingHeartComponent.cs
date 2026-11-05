using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Items;

[RegisterComponent, AutoGenerateComponentPause, NetworkedComponent]
public sealed partial class HereticLivingHeartComponent : Component
{
    [DataField]
    public EntityUid? Target;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextHeartbeat;

    [DataField]
    public float MinInterval = 0.35f;

    [DataField]
    public float MaxInterval = 3.0f;

    [DataField]
    public float TrackRange = 30f;
}
