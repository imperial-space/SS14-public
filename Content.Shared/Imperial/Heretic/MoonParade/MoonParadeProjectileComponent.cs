using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Heretic.MoonParade;

[RegisterComponent]
public sealed partial class MoonParadeProjectileComponent : Component
{
    [DataField]
    public EntityUid Shooter = EntityUid.Invalid;

    [DataField]
    public int MaxBounces = 40;

    [DataField]
    public int BounceCount = 0;

    [DataField]
    public float Speed = 4f;

    // Runtime only — not networked
    public HashSet<EntityUid> HitMobs = new();
}
