using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticAshOrbComponent : Component
{
    public EntityUid HereticUid;

    [DataField]
    public bool Empowered;
}
