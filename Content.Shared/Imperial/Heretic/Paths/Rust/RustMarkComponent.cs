using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Paths.Rust;

/// <summary>
/// Mark of Corruption: applied by a Mansus Grasp hit when Mark of Rust is known. A follow-up hit with
/// the Rusty Blade on a marked target consumes the mark and staggers/stuns it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RustMarkComponent : Component
{
    [DataField]
    public SpriteSpecifier MarkSprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/tag.rsi"), "rust");
}
