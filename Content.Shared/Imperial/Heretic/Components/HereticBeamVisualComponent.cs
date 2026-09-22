using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticBeamVisualComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Length;
}
