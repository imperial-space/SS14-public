using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Kinetic Crusher upgrade dropped by Mega Legion.
/// Each melee hit also marks nearby enemies within <see cref="MarkRadius"/> tiles of the target.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherHunterEyeUpgradeComponent : Component
{
    /// <summary>Radius around the hit target in which additional enemies are also marked.</summary>
    [DataField]
    public float MarkRadius = 3f;
}
