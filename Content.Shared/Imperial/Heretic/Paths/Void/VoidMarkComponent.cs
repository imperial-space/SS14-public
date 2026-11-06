using Content.Shared.Imperial.Heretic.Core;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Paths.Void;

/// <summary>
/// Void mark applied by Mansus Grasp on a Void path heretic. Consumed by the Seeking Blade ability.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VoidMarkComponent : Component, IHereticMarkComponent
{
    [DataField]
    public SpriteSpecifier MarkSprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/tag.rsi"), "void");

    [DataField]
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromSeconds(15);

    [ViewVariables]
    public TimeSpan ExpireTime { get; set; }
}
