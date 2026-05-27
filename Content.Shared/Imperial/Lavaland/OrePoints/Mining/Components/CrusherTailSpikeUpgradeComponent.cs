using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Kinetic Crusher upgrade dropped by Urs.
/// Deals bonus damage on the very first hit against a target (before the crusher mark is applied).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherTailSpikeUpgradeComponent : Component
{
    /// <summary>Extra damage dealt when the target has no crusher mark yet.</summary>
    [DataField(required: true)]
    public DamageSpecifier BonusDamage = new();
}
