using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticRustPassiveComponent : Component
{
    public TimeSpan TickInterval = TimeSpan.FromSeconds(2);
    public TimeSpan NextTick;
}
