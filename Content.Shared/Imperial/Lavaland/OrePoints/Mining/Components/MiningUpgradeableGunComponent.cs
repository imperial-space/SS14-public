using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Allows a gun to accept mining PKA upgrades using a percentage-based capacity system.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MiningUpgradeableGunComponent : Component
{
    /// <summary>
    /// Total capacity (100 = 100%). Each upgrade costs a certain amount of capacity.
    /// </summary>
    [DataField]
    public int MaxCapacity = 100;

    /// <summary>
    /// Id of the container that holds upgrades.
    /// </summary>
    [DataField]
    public string UpgradesContainerId = "mining_upgrades";

    /// <summary>
    /// Whitelist for what upgrades can be inserted.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Sound played when an upgrade is inserted.
    /// </summary>
    [DataField]
    public SoundSpecifier? InsertSound;

    /// <summary>
    /// Localized string key for the capacity display in examine.
    /// </summary>
    [DataField]
    public LocId ExamineCapacityText = "mining-upgradeable-gun-examine-capacity";

    /// <summary>
    /// Base recharge cooldown in seconds, before upgrades are applied.
    /// Set to the same value as RechargeBasicEntityAmmoComponent.RechargeCooldown.
    /// Used to recalculate cooldown when recharge-reduction upgrades are installed.
    /// </summary>
    [DataField]
    public float BaseRechargeCooldown = 3f;
}
