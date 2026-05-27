using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Kinetic Crusher upgrade dropped by Colossus.
/// Deals bonus radiation damage to targets that already carry the crusher mark.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherBlasterTubesUpgradeComponent : Component
{
    /// <summary>Extra damage applied to marked targets.</summary>
    [DataField(required: true)]
    public DamageSpecifier BonusDamage = new();
}
