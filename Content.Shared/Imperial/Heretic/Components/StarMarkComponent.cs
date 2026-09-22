using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class StarMarkComponent : Component
{
    [DataField]
    public EntityUid? OverlayEntity;
}
