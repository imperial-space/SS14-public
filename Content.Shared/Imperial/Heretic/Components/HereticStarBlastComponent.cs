using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticStarBlastComponent : Component
{
    [DataField]
    public EntityUid? ActiveProjectile;

    public EntityUid? StarBlastAction;
}
