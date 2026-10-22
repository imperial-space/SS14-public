using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Items;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticFleshWeaveItemComponent : Component
{
    [DataField]
    public EntityUid HereticUid;

    public EntityUid? PendingOrgan;
    public EntityUid? PendingTarget;
}
