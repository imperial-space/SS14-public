using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Kinetic Crusher upgrade dropped by Ash Drake.
/// Replaces the destabilizer charge with an incendiary variant that ignites targets on hit.
/// Takes priority over Watcher Wing in ammo selection.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherAshenSkullUpgradeComponent : Component
{
    /// <summary>Projectile prototype to use when this upgrade is installed.</summary>
    [DataField]
    public EntProtoId UpgradedProjectile = "BulletChargeAshen";
}
