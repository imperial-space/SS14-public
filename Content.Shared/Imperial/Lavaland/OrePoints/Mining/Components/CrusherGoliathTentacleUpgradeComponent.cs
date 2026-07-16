using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Upgrade: Щупальце Голиафа.
/// The lower the wielder's health, the more bonus damage the crusher deals.
/// Bonus = <see cref="MaxBonusDamage"/> * (1 - currentHP/maxHP).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherGoliathTentacleUpgradeComponent : Component
{
    /// <summary>Bonus damage at 0 HP (scales linearly down to 0 at full HP).</summary>
    [DataField(required: true)]
    public DamageSpecifier MaxBonusDamage = new();
}
