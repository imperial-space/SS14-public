using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Upgrade: Крыло Наблюдателя.
/// Replaces the crusher's destabiliser projectile with <see cref="UpgradedProjectile"/>,
/// which applies a brief stun on collision.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherWatcherWingUpgradeComponent : Component
{
    /// <summary>Projectile prototype to use while this upgrade is installed.</summary>
    [DataField]
    public EntProtoId UpgradedProjectile = "BulletChargeWatcher";
}
