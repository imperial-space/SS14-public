using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// When installed on a mining PKA, fired bullets gain life-steal against tagged targets.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LifeStealGunUpgradeComponent : Component
{
    [DataField]
    public float HealAmount = 2.5f;

    [DataField]
    public ProtoId<TagPrototype> TargetTag = "LavalandMob";
}
