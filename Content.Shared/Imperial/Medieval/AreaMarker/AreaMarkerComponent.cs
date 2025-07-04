using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.AreaMarker;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AreaMarkerComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier AudioPath { get; set; } = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public string AreaName { get; set; } = "area-name-placeholder";

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int FontSize { get; set; } = 24;
}
