using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Void mark applied by Mansus Grasp on a Void path heretic. Consumed by the Seeking Blade ability.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VoidMarkComponent : Component
{
    [DataField]
    public SpriteSpecifier MarkSprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/tag.rsi"), "void");
}
