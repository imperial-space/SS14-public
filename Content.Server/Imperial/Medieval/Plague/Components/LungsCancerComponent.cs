using Content.Shared.Destructible.Thresholds;

namespace Content.Server.Imperial.Medieval.Plague;

[RegisterComponent]
public sealed partial class LungsCancerComponent : Component
{
    [DataField]
    public MinMax Delay;

    [DataField]
    public MinMax Duration;

    public bool Active = false;

    public TimeSpan NextEffect = TimeSpan.Zero;

    public TimeSpan EndTime = TimeSpan.Zero;
}
