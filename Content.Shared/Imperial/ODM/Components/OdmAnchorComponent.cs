using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.ODM.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class OdmAnchorComponent : Component
{
    [DataField]
    public EntityUid Gear;

    [DataField]
    public OdmHookSide Side;

    [DataField]
    public float DesiredLength;
}