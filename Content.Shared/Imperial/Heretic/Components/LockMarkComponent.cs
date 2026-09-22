using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class LockMarkComponent : Component
{
    /// <summary>
    /// The ID card whose access was disabled when this mark was applied.
    /// Stored so access can be restored if the mark is removed.
    /// </summary>
    [DataField]
    public EntityUid? IdCard;
}
