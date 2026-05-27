namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Mining PKA upgrade that reduces the recharge cooldown of RechargeBasicEntityAmmo.
/// </summary>
[RegisterComponent]
public sealed partial class GunUpgradeRechargeReductionComponent : Component
{
    /// <summary>
    /// How many seconds to subtract from the gun's recharge cooldown.
    /// </summary>
    [DataField]
    public float ReductionSeconds = 2f;
}
