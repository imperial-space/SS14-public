namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class TimedBossHealComponent : Component
{
    [DataField]
    public float Duration = 15f;

    [DataField]
    public float HealAmount = 5f;

    public TimeSpan HealTime = TimeSpan.Zero;
}
