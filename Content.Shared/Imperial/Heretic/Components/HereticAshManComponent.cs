using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticAshManComponent : Component
{
    public EntityUid CasterUid = EntityUid.Invalid;

    [DataField]
    public float Lifetime = 60f;
    public float LifetimeElapsed = 0f;

    [DataField]
    public float MoveSpeed = 4.5f;

    [DataField]
    public float AttackRange = 1.3f;

    [DataField]
    public float AttackCooldown = 1.2f;
    public float AttackTimer = 0f;

    [DataField]
    public float FireTrailInterval = 0.6f;
    public float FireTrailTimer = 0f;
}
