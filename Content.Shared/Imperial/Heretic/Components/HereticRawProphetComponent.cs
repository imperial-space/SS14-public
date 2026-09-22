using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticRawProphetComponent : Component
{
    [DataField]
    public EntityUid Master = EntityUid.Invalid;
}
