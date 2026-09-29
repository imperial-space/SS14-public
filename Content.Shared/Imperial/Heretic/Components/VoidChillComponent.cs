using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class VoidChillComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stacks = 0;

    [DataField]
    public SpriteSpecifier PartialSprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/void.rsi"), "void_chill_partial");

    [DataField]
    public SpriteSpecifier MaxStacksSprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/void.rsi"), "void_chill_oh_fuck");
}
