using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Ash;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticAshOrbComponent : Component
{
    public EntityUid HereticUid;

    [DataField]
    public bool Empowered;
}
