using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Ash;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticAshManComponent : Component
{
    [ViewVariables]
    public EntityUid CasterUid = EntityUid.Invalid;

    [DataField]
    public float Lifetime = 60f;
    [ViewVariables]
    public float LifetimeElapsed = 0f;

    [DataField]
    public float MoveSpeed = 4.5f;

    [DataField]
    public float AttackRange = 1.3f;

    [DataField]
    public float AttackCooldown = 1.2f;
    [ViewVariables]
    public float AttackTimer = 0f;

    [DataField]
    public float FireTrailInterval = 0.6f;
    [ViewVariables]
    public float FireTrailTimer = 0f;
}
