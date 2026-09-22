using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticFireSharkComponent : Component
{
    public EntityUid TargetUid = EntityUid.Invalid;

    [DataField] public float Lifetime = 25f;
    public float LifetimeElapsed = 0f;

    [DataField] public float MoveSpeed = 7f;
    [DataField] public float AttackRange = 1.3f;
    [DataField] public float AttackCooldown = 0.6f;
    public float AttackTimer = 0f;

    [DataField] public float FireTrailInterval = 0.35f;
    public float FireTrailTimer = 0f;

    [DataField] public float RegenInterval = 1f;
    [DataField] public float RegenAmount = 1f;
    public float RegenTimer = 0f;
}
