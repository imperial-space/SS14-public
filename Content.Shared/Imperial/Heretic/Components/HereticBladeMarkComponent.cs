using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Mark of the Blade: target is confined to their current room until the heretic lands a follow-up
/// Mansus Grasp on them, which consumes the mark and grants the heretic an orbiting blade.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HereticBladeMarkComponent : Component
{
    [DataField]
    public EntityUid Heretic = EntityUid.Invalid;

    [DataField]
    public List<EntityUid> LockedDoors = new();

    [DataField]
    public SpriteSpecifier MarkSprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/tag.rsi"), "blade");
}
