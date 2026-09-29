using Content.Shared.Imperial.Heretic.Core;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Paths.Lock;

[RegisterComponent, NetworkedComponent]
public sealed partial class LockMarkComponent : Component, IHereticMarkComponent
{
    /// <summary>
    /// The ID card whose access was disabled when this mark was applied.
    /// Stored so access can be restored if the mark is removed.
    /// </summary>
    [DataField]
    public EntityUid? IdCard;

    [DataField]
    public SpriteSpecifier MarkSprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/heretic/tag.rsi"), "lock");

    [DataField]
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromSeconds(15);

    [ViewVariables]
    public TimeSpan ExpireTime { get; set; }
}
