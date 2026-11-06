using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles.ClownCar;

/// <summary>Сплющенный машиной (/datum/element/squish).</summary>
[RegisterComponent]
public sealed partial class ImperialClownCarSquishedComponent : Component
{
    [ViewVariables]
    public TimeSpan EndAt;
    [ViewVariables]
    public System.Numerics.Vector2 OldScale = System.Numerics.Vector2.One;
}
