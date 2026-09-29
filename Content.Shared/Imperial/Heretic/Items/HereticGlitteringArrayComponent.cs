using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Items;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticGlitteringArrayComponent : Component
{
    [DataField]
    public EntityUid? Wearer;
}
