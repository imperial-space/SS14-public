using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticMoonParadeProjectileComponent : Component
{
    [DataField]
    public EntityUid Shooter;

    [DataField]
    public int BounceCount = 0;

    [DataField]
    public int MaxBounces = 40;

    [DataField]
    public float Speed = 2f;

    public HashSet<EntityUid> MobsHit = new();
}
