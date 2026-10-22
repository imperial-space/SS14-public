using Content.Shared.Imperial.Heretic.Core;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Paths.Ash;

[RegisterComponent, NetworkedComponent]
public sealed partial class AshMarkComponent : Component, IHereticMarkComponent
{
    [DataField]
    public SpriteSpecifier MarkSprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/tag.rsi"), "ash");

    [DataField]
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromSeconds(15);

    [ViewVariables]
    public TimeSpan ExpireTime { get; set; }
}
