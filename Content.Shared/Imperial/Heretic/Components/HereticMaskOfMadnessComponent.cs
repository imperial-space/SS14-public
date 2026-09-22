using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Worn Mask of Madness: periodically applies stamina damage and auditory hallucinations
/// to all entities within range. When forced onto a non-believer, cannot be removed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HereticMaskOfMadnessComponent : Component
{
    [DataField]
    public float AoeRadius = 7.5f;

    /// <summary>
    /// Stamina damage per tick (30% chance, only if target's current loss is at most 85).
    /// </summary>
    [DataField]
    public float StaminaDamagePerTick = 10f;

    [DataField]
    public float TickInterval = 2.5f;

    [DataField]
    public float TickAccumulator = 0f;

    /// <summary>
    /// If true, this mask was forced onto a non-believer and cannot be removed by them.
    /// </summary>
    [DataField]
    public bool Locked = false;
}
