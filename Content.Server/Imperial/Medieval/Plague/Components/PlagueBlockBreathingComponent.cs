namespace Content.Server.Imperial.Medieval.Plague;

[RegisterComponent]
public sealed partial class PlagueBlockBreathingComponent : Component
{
    [DataField]
    public TimeSpan EndTime = TimeSpan.Zero;
}
