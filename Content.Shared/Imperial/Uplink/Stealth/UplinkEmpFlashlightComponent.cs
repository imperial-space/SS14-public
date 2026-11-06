using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>ЭМИ-фонарик (flashlight/emp).</summary>
[RegisterComponent]
public sealed partial class UplinkEmpFlashlightComponent : Component
{
    [DataField]
    public int MaxCharges = 4;

    [DataField]
    public int Charges = 4;

    [DataField]
    public TimeSpan ChargeDelay = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public TimeSpan NextCharge;

    [DataField]
    public TimeSpan EmpFlashlightInteractDuration = TimeSpan.FromSeconds(20);

    [DataField]
    public float EmpFlashlightInteractEnergyConsumption = 100000f;
}
