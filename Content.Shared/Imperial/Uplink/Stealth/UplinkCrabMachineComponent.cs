using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Машина CRAB-17: выкачивает деньги станции, пока работает.</summary>
[RegisterComponent]
public sealed partial class UplinkCrabMachineComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(10);

    /// <summary>Доля средств, уходящая за один цикл.</summary>
    [DataField]
    public float Fraction = 0.05f;

    [ViewVariables]
    public int Stolen;

    [ViewVariables]
    public TimeSpan EndTime;

    [ViewVariables]
    public TimeSpan NextDrain;

    [ViewVariables]
    public EntityUid? OwnerEntity;
}
