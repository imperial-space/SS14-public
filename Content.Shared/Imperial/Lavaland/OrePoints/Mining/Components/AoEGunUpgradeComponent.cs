using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// When placed on a gun upgrade entity, causes fired bullets to explode in a small area on impact.
/// The explosion is created by <see cref="AoEBulletComponent"/> added to each shot bullet.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AoEGunUpgradeComponent : Component
{
    [DataField]
    public string ExplosionType = "Default";

    [DataField]
    public float TotalIntensity = 4f;

    [DataField]
    public float Slope = 5f;

    [DataField]
    public float MaxIntensity = 4f;
}
