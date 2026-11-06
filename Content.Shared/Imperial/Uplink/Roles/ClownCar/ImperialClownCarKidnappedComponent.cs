using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles.ClownCar;

/// <summary>Похищенный пассажир машины (VEHICLE_CONTROL_KIDNAPPED).</summary>
[RegisterComponent]
public sealed partial class ImperialClownCarKidnappedComponent : Component
{
    [ViewVariables]
    public EntityUid Car;
    [ViewVariables]
    public EntityUid? ThankAction;
    [ViewVariables]
    public EntityUid? ExitAction;
}
