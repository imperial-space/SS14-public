using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticMaidMirrorBallComponent : Component
{
    public EntityUid MaidUid;

    [DataField] public float HealInterval = 1f;
    [DataField] public float HealAmount = 5f;
    public float HealTimer = 0f;
}
