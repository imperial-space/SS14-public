using System.Runtime.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Helm;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class SteeringOarComponent : Component
{

    [DataField("power")]
    public float Power { get; set; } = 10f;
}
