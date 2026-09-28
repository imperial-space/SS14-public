using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class CosmosMarkComponent : Component
{
    [DataField]
    public EntityUid? AnchorEntity;
}
