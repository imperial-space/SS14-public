using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Kinetic Crusher upgrade dropped by Bubblegum.
/// Each melee hit on a matching tag target heals the wielder.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherDemonClawsUpgradeComponent : Component
{
    /// <summary>HP healed per melee hit (applied as negative Blunt damage).</summary>
    [DataField]
    public float HealAmount = 5f;

    /// <summary>Only steal life from targets with this tag. Null = any target.</summary>
    [DataField]
    public string? TargetTag = "LavalandMob";
}
