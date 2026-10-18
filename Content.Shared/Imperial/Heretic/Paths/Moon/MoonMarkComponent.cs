using Content.Shared.Imperial.Heretic.Core;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Paths.Moon;

/// <summary>
/// status_effect/eldritch/moon: метка Хватки Луны. Пацифицирует, пока цель не получит 15 урона,
/// и подрывается лунным клинком.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MoonMarkComponent : Component, IHereticMarkComponent
{
    [DataField]
    public SpriteSpecifier MarkSprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/eldritch_fx.rsi"), "moon_insanity_overlay");

    [DataField]
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromSeconds(15);

    [ViewVariables]
    public TimeSpan ExpireTime { get; set; }

    /// <summary>Пацификацию повесила именно метка — её и снимать.</summary>
    [ViewVariables]
    public bool AddedPacified;

    [ViewVariables]
    public float DamageSustained;
}
