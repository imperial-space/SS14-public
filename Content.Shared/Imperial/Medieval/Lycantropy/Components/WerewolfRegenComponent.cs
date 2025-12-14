using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Lycantropy;

[RegisterComponent, NetworkedComponent]
public sealed partial class WerewolfRegenComponent : Component
{
    public TimeSpan NextUpdate = TimeSpan.Zero;
}
