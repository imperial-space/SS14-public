using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Ash;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticFireSharkComponent : Component
{
    [ViewVariables]
    public EntityUid TargetUid = EntityUid.Invalid;

    [DataField] public float Lifetime = 25f;
    [ViewVariables]
    public float LifetimeElapsed = 0f;

    [DataField] public float MoveSpeed = 7f;
    [DataField] public float AttackRange = 1.3f;
    [DataField] public float AttackCooldown = 0.6f;
    [ViewVariables]
    public float AttackTimer = 0f;

    [DataField] public float FireTrailInterval = 0.35f;
    [ViewVariables]
    public float FireTrailTimer = 0f;

    [DataField] public float RegenInterval = 1f;
    [DataField] public float RegenAmount = 1f;
    [ViewVariables]
    public float RegenTimer = 0f;
}
