using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Applied by Rusting Crown: deals periodic caustic damage (DoT) and marks the target
/// for increased rust path damage. Ticks 6 times (once per 5 seconds = 30s total).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HereticRustingCrownMarkComponent : Component
{
    [DataField]
    public TimeSpan NextTick = TimeSpan.Zero;

    [DataField]
    public int TicksRemaining = 6;

    /// <summary>Caustic damage applied per tick.</summary>
    [DataField]
    public float DamagePerTick = 5f;
}
