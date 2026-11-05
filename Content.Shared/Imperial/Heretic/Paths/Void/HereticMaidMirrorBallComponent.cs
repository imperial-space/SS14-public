using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Void;

[RegisterComponent, AutoGenerateComponentPause, NetworkedComponent]
public sealed partial class HereticMaidMirrorBallComponent : Component
{
    [ViewVariables]
    public EntityUid MaidUid;

    [DataField] public float HealInterval = 1f;
    [DataField] public float HealAmount = 5f;
    [ViewVariables, AutoPausedField]
    public TimeSpan NextHeal;
}
