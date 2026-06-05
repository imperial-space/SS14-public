using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Applied to bullets by <see cref="LifeStealGunUpgradeComponent"/> during shot relay.
/// </summary>
[RegisterComponent]
public sealed partial class LifeStealBulletComponent : Component
{
    public float HealAmount;
    public ProtoId<TagPrototype> TargetTag = "LavalandMob";
}
