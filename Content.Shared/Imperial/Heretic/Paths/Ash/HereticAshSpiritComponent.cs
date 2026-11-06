using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Ash;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticAshSpiritComponent : Component
{
    [DataField]
    public TimeSpan ShiftDelay = TimeSpan.FromSeconds(1.1);
}
