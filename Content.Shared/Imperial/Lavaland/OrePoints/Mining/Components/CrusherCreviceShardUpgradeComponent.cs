using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Kinetic Crusher upgrade dropped by Zlatozhor.
/// Deals bonus piercing damage that ignores resistances on every melee hit.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherCreviceShardUpgradeComponent : Component
{
    /// <summary>Extra damage applied with <c>ignoreResistances: true</c>.</summary>
    [DataField(required: true)]
    public DamageSpecifier PiercingBonus = new();
}
