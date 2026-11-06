namespace Content.Shared.Imperial.Heretic.Rituals;

[RegisterComponent]
public sealed partial class HereticMadnessRuneComponent : Component
{
    [DataField]
    public TimeSpan TriggeredTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan TriggeredTime2 = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan TriggeredDelay = TimeSpan.FromMilliseconds(15000);

    [DataField]
    public TimeSpan TriggeredDelay2 = TimeSpan.FromMilliseconds(10000);
}
