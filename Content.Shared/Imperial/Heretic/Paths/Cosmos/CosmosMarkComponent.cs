using Content.Shared.Imperial.Heretic.Core;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Paths.Cosmos;

[RegisterComponent, NetworkedComponent]
public sealed partial class CosmosMarkComponent : Component, IHereticMarkComponent
{
    [DataField]
    public EntityUid? AnchorEntity;

    [DataField]
    public SpriteSpecifier MarkSprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/tag.rsi"), "cosmos");

    [DataField]
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromSeconds(15);

    [ViewVariables]
    public TimeSpan ExpireTime { get; set; }
}
