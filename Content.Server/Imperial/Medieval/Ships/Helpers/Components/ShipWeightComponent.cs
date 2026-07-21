namespace Content.Server.Imperial.Medieval.Ships.PlayerDrowning;

[RegisterComponent]
public sealed partial class ShipWeightComponent : Component
{
    [DataField("OverloadCeilPerTile")]
    public float OverloadCeilPerTile = 20f;
}
