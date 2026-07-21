namespace Content.Shared.Imperial.Medieval.Ships.Compass;

[RegisterComponent]
public sealed partial class MedievalCompassComponent : Component
{
    public static readonly string[] DirectionStates =
    [
        "compass-e",
        "compass-ne",
        "compass-n",
        "compass-nw",
        "compass-w",
        "compass-sw",
        "compass-s",
        "compass-se",
    ];
}
