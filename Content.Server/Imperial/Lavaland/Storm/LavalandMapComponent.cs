namespace Content.Server.Imperial.Lavaland.Storm;

[RegisterComponent]
public sealed partial class LavalandMapComponent : Component
{
    [DataField]
    public float MinStormInterval = 480f; // 8 minutes

    [DataField]
    public float MaxStormInterval = 720f; // 12 minutes

    [DataField]
    public float WarningDuration = 30f;

    [DataField]
    public float StormDuration = 120f; // 2 minutes

    [DataField]
    public float DamageInterval = 2f;

    [DataField]
    public float StormEndingDuration = 5f;

    [DataField]
    public float NoEventChance = 0.1f;

    [DataField]
    public float PassByChance = 0.1f;

    // Runtime state
    public float StormTimer;
    public LavalandStormState StormState = LavalandStormState.Idle;
    public float StateTimer;
    public float DamageTimer;
}

public enum LavalandStormState : byte
{
    Idle,
    Warning,
    PassingBy,
    Active,
    Ending,
}
