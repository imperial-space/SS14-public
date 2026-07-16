using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Rescue capsule — when used, deploys a mining base grid at the user's location after a delay.
/// Requires a 7x7 tile area to be clear of walls and chasms.
/// </summary>
[RegisterComponent]
public sealed partial class RescueCapsuleComponent : Component
{
    /// <summary>
    /// Delay in seconds before the capsule deploys.
    /// </summary>
    [DataField]
    public float DeployDelay = 8f;

    /// <summary>
    /// Grid path to load directly (e.g. /Imperial/Lavaland/mining.yml).
    /// </summary>
    [DataField]
    public string GridPath = "/Imperial/Lavaland/mining.yml";
}

[Serializable, NetSerializable]
public sealed partial class RescueCapsuleDoAfterEvent : SimpleDoAfterEvent
{
}
