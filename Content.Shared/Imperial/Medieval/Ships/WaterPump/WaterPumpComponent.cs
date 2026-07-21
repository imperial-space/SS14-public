using System.Runtime.Serialization;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.WaterPump;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class WaterPumpComponent : Component
{
    [DataField("waterCount")]
    public float WaterCount = 250f;

    [DataField("user")]
    public EntityUid? User;

    [DataField("usedTime")]
    public TimeSpan? UsedTime;

    [DataField("doAfter")]
    public DoAfterId? DoAfter;

}
