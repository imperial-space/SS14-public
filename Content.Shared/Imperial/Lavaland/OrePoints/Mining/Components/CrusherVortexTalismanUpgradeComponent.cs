using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Kinetic Crusher upgrade dropped by Hierophant.
/// Each melee hit throws the target away from the wielder (knockback).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherVortexTalismanUpgradeComponent : Component
{
    /// <summary>Throw speed applied to the target.</summary>
    [DataField]
    public float KnockbackStrength = 5f;

    /// <summary>Maximum travel distance of the knockback throw.</summary>
    [DataField]
    public float KnockbackDistance = 3f;
}
