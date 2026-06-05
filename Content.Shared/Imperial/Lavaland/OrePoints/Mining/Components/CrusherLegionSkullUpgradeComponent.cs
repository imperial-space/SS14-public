using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Upgrade: Череп Легиона.
/// Reduces the crusher's shot UseDelay by <see cref="RechargeReduction"/> seconds.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherLegionSkullUpgradeComponent : Component
{
    [DataField]
    public float RechargeReduction = 0.3f;
}
