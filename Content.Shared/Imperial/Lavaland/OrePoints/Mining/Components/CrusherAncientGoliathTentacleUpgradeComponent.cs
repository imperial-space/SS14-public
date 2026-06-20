using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Upgrade: Большое глазастое щупальце.
/// Deals <see cref="BonusMultiplier"/> × base weapon damage as extra damage
/// when the target has more than <see cref="HealthThreshold"/> of its max HP remaining
/// AND matches <see cref="RequiredTag"/> (if set).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherAncientGoliathTentacleUpgradeComponent : Component
{
    /// <summary>
    /// Fraction of max HP the target must have remaining to trigger the bonus.
    /// 0.9 = target must have over 90% HP.
    /// </summary>
    [DataField]
    public float HealthThreshold = 0.9f;

    /// <summary>Bonus multiplier applied to the crusher's base melee damage. 0.5 = +50%.</summary>
    [DataField]
    public float BonusMultiplier = 0.5f;

    /// <summary>Tag the target must have. Null = no restriction.</summary>
    [DataField]
    public string? RequiredTag = "LavalandMob";
}
