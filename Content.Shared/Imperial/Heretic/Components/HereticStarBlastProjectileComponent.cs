using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticStarBlastProjectileComponent : Component
{
    [DataField]
    public EntityUid Shooter = EntityUid.Invalid;

    [DataField]
    public float Speed = 2f;

    [DataField]
    public float TrailTimer = 0f;

    [DataField]
    public float TrailInterval = 0.3f;
}
