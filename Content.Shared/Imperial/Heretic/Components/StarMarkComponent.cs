using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class StarMarkComponent : Component
{
    [DataField]
    public EntityUid? OverlayEntity;

    [DataField]
    public SpriteSpecifier MarkSprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/eldritch_fx.rsi"), "cosmic_ring");
}
