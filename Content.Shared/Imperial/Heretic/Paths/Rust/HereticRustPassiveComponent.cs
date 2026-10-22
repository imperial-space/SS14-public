namespace Content.Shared.Imperial.Heretic.Paths.Rust;

[RegisterComponent]
public sealed partial class HereticRustPassiveComponent : Component
{
    public TimeSpan TickInterval = TimeSpan.FromSeconds(2);
    public TimeSpan NextTick;
}
