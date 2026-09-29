using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticWeeepingHallucinationStatusEffectComponent : Component
{
    [DataField]
    public TimeSpan NextHallucinationTime = TimeSpan.Zero;

    [DataField]
    public float MinInterval = 120f;

    [DataField]
    public float MaxInterval = 480f;

    // ─── Визуал галлюцинации: окружающие видятся еретиками ────────────────────

    [DataField]
    public SpriteSpecifier DefaultArmorSprite =
        new SpriteSpecifier.Rsi(new ResPath("Imperial/Other/Heretic/Clothes/HereticRobe.rsi"), "equipped-OUTERCLOTHING");

    [DataField]
    public SpriteSpecifier DefaultHoodSprite =
        new SpriteSpecifier.Rsi(new ResPath("Imperial/Other/Heretic/Clothes/HereticHelmet.rsi"), "equipped-HELMET");

    [DataField]
    public SpriteSpecifier MoonArmorSprite =
        new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/heretic_robes.rsi"), "moon_armor_worn");

    [DataField]
    public SpriteSpecifier MoonHoodSprite =
        new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/heretic_hoods.rsi"), "moon_armor_worn");

    [DataField]
    public SpriteSpecifier MoonBladeSprite =
        new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/blade_moon_inhand.rsi"), "moon_blade-inhand-right");

    /// <summary>Шанс, что лунный еретик из галлюцинации держит клинок.</summary>
    [DataField]
    public float MoonBladeChance = 0.5f;
}
