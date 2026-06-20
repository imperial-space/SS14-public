namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Marks a bullet as AoE — it will create an explosion on hit.
/// Set by <see cref="AoEGunUpgradeComponent"/> during GunShotEvent relay.
/// </summary>
[RegisterComponent]
public sealed partial class AoEBulletComponent : Component
{
    public string ExplosionType = "Default";
    public float TotalIntensity;
    public float Slope;
    public float MaxIntensity;
}
