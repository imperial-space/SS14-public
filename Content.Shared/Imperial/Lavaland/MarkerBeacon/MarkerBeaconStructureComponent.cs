using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.MarkerBeacon;

[RegisterComponent, NetworkedComponent]
public sealed partial class MarkerBeaconStructureComponent : Component
{
    [DataField]
    public MarkerBeaconColor Color = MarkerBeaconColor.Burgundy;
}
