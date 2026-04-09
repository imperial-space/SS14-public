namespace Content.Server.Imperial.SCP.SCPFireman.Components;

[RegisterComponent]
public sealed partial class SCPFireSpreadComponent : Component
{
    [DataField("owner")]
    public EntityUid FireOwner;

    [DataField("spreadDelay")]
    public TimeSpan SpreadDelay = TimeSpan.FromSeconds(15);

    [DataField("spreadInterval")]
    public TimeSpan SpreadInterval = TimeSpan.FromSeconds(15);

    [DataField("pointInterval")]
    public TimeSpan PointInterval = TimeSpan.FromSeconds(1);

    [DataField("healInterval")]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(5);

    [DataField("healPerType")]
    public float HealPerType = 0.1f;

    [DataField("igniteInterval")]
    public TimeSpan IgniteInterval = TimeSpan.FromSeconds(1);

    [DataField("igniteRadius")]
    public float IgniteRadius = 0.7f;

    [DataField("maxSpreadPerPulse")]
    public int MaxSpreadPerPulse = 4;

    [ViewVariables]
    public TimeSpan NextSpreadTime;

    [ViewVariables]
    public TimeSpan NextPointTime;

    [ViewVariables]
    public TimeSpan NextHealTime;

    [ViewVariables]
    public TimeSpan NextIgniteTime;
}
