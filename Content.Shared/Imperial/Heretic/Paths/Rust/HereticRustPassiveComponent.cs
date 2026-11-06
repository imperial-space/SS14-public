namespace Content.Shared.Imperial.Heretic.Paths.Rust;

[RegisterComponent]
public sealed partial class HereticRustPassiveComponent : Component
{
    [ViewVariables]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(2);
    [ViewVariables]
    public TimeSpan NextTick;
}
