using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.CartographerTable;

[RegisterComponent, NetworkedComponent]
public sealed partial class CartographerRadarMarkerComponent : Component
{
    [DataField("color")]
    public Color Color = Color.White;

    [DataField("radius")]
    public float Size = 1f;

    [DataField]
    public bool ZoomScaling = false;

    [DataField]
    public string? RsiPath = "/Textures/Imperial/Medieval/Ships/CartographerTableMarkers.rsi";

    [DataField]
    public string? State { get; private set; } = "circle32x32";
}
