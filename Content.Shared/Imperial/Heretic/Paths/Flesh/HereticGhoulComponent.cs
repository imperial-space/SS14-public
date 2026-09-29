using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Flesh;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticGhoulComponent : Component
{
    [DataField]
    public EntityUid Master;
}
