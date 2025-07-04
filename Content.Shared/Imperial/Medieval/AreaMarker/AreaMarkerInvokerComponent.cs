using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.AreaMarker;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AreaMarkerInvokerComponent : Component
{
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public string? LastArea;
}
