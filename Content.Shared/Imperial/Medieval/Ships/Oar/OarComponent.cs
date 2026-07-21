using System.Numerics;

namespace Content.Shared.Imperial.Medieval.Ships.Oar;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class OarComponent : Component
{
    [DataField]
    public int Power = 10;

    [DataField]
    public int SpeedModifier = 1;

    [DataField]
    public float MinWeight = 10f;

    [DataField("OverloadCeilPerTile")]
    public float OverloadCeilPerTile = 20f;

    [DataField]
    public Angle Direction;
}
