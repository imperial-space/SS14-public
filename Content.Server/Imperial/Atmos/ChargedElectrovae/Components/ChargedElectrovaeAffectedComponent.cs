namespace Content.Server.Imperial.Atmos.ChargedElectrovae.Components;

[RegisterComponent]
public sealed partial class ChargedElectrovaeAffectedComponent : Component
{
    [DataField]
    public float? OriginalBatteryMaxCharge;

    [DataField]
    public float? ChargedElectrovaeChargeRate;

    [DataField]
    public bool? OriginalNeedsPower;
}
