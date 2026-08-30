using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Aquila.Backflip;

[RegisterComponent, NetworkedComponent]
public sealed partial class BackflipEmoteComponent : Component
{
    public TimeSpan PhaseEndTime;

    [DataField]
    public TimeSpan PhaseDuration = TimeSpan.FromSeconds(0.5);

    [DataField]
    public float StaminaCostFraction = 0.075f;

    [DataField]
    public float SpeedMultiplier = 1.25f;

    public HashSet<string> ClearedFixtures = new();
}
