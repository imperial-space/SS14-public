using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class CosmosMarkComponent : Component
{
    [DataField]
    public EntityUid? AnchorEntity;

    [DataField]
    public SpriteSpecifier MarkSprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/tag.rsi"), "cosmos");
}
