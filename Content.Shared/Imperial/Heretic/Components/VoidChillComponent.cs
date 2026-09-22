using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class VoidChillComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stacks = 0;
}
