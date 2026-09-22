using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticLivingHeartComponent : Component
{
    [DataField]
    public EntityUid Target = EntityUid.Invalid;

    [DataField]
    public float HeartbeatTimer = 0f;

    [DataField]
    public float MinInterval = 0.35f;

    [DataField]
    public float MaxInterval = 3.0f;

    [DataField]
    public float TrackRange = 30f;
}
