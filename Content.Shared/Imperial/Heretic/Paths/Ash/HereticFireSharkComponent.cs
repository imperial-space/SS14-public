using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Ash;

[RegisterComponent, AutoGenerateComponentPause, NetworkedComponent]
public sealed partial class HereticFireSharkComponent : Component
{
    [ViewVariables]
    public EntityUid? TargetUid;

    [DataField] public float Lifetime = 25f;
    [ViewVariables, AutoPausedField]
    public TimeSpan LifetimeEnd;

    [DataField] public float MoveSpeed = 7f;
    [DataField] public float AttackRange = 1.3f;
    [DataField] public float AttackCooldown = 0.6f;
    [ViewVariables, AutoPausedField]
    public TimeSpan NextAttack;

    [DataField] public float FireTrailInterval = 0.35f;
    [ViewVariables, AutoPausedField]
    public TimeSpan NextFireTrail;

    [DataField] public float RegenInterval = 1f;
    [DataField] public float RegenAmount = 1f;
    [ViewVariables, AutoPausedField]
    public TimeSpan NextRegen;

    /// <summary>Цель, найденная последним поиском (поиск по области — не чаще раза в полсекунды).</summary>
    [ViewVariables]
    public EntityUid? CachedTarget;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextRetarget;
}
