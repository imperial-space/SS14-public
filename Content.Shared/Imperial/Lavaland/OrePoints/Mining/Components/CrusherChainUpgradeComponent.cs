using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Kinetic Crusher upgrade dropped by Dead Miner.
/// Every melee hit temporarily slows the target's movement speed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherChainUpgradeComponent : Component
{
    /// <summary>How long the movement slow lasts.</summary>
    [DataField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(2.0);

    /// <summary>Walk speed multiplier while slowed (0..1, lower = slower).</summary>
    [DataField]
    public float WalkSpeedModifier = 0.5f;

    /// <summary>Sprint speed multiplier while slowed (0..1, lower = slower).</summary>
    [DataField]
    public float SprintSpeedModifier = 0.5f;
}
