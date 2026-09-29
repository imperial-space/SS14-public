using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Paths.Moon;

[RegisterComponent, NetworkedComponent]
public sealed partial class MoonMarkComponent : Component
{
    [DataField]
    public SpriteSpecifier MarkSprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/eldritch_fx.rsi"), "moon_insanity_overlay");
}
