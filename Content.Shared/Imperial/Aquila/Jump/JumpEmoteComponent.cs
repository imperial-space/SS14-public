using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Aquila.Jump;

[RegisterComponent, NetworkedComponent]
public sealed partial class JumpEmoteComponent : Component
{
    public TimeSpan PhaseEndTime;

    [DataField]
    public TimeSpan PhaseDuration = TimeSpan.FromSeconds(0.5);

    [DataField]
    public float StaminaCostFraction = 0.05f;

    [DataField]
    public float SpeedMultiplier = 1.35f;

    public HashSet<string> ClearedFixtures = new();
}
