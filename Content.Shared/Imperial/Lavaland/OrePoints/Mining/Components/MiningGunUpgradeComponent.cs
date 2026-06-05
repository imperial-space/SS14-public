using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Marks an item as a mining PKA upgrade with a capacity cost.
/// Used together with GunUpgradeComponent for actual stat modifications.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MiningGunUpgradeComponent : Component
{
    /// <summary>
    /// How much capacity this upgrade uses (out of MiningUpgradeableGunComponent.MaxCapacity).
    /// </summary>
    [DataField]
    public int SlotCost = 10;
}
