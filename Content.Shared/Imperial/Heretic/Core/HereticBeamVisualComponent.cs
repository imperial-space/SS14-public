using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Core;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticBeamVisualComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Length;
}
