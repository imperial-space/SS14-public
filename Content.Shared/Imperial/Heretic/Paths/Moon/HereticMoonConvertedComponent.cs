using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Paths.Moon;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticMoonConvertedComponent : Component
{
    public float DamageAccumulated = 0f;
    public const float DamageBreakThreshold = 75f;

    [DataField]
    public SpriteSpecifier OverlaySprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/eldritch_fx.rsi"), "moon_insanity_overlay");
}
