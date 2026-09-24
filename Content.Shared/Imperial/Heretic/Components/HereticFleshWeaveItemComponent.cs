using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticFleshWeaveItemComponent : Component
{
    [DataField]
    public EntityUid HereticUid;

    public EntityUid? PendingOrgan;
    public EntityUid? PendingTarget;
}
