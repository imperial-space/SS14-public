using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Ash;

[RegisterComponent, AutoGenerateComponentPause, NetworkedComponent]
public sealed partial class HereticAshManComponent : Component
{
    [ViewVariables]
    public EntityUid? CasterUid;

    [DataField]
    public float Lifetime = 60f;
    [ViewVariables, AutoPausedField]
    public TimeSpan LifetimeEnd;

    [DataField]
    public float MoveSpeed = 4.5f;

    [DataField]
    public float AttackRange = 1.3f;

    [DataField]
    public float AttackCooldown = 1.2f;
    [ViewVariables, AutoPausedField]
    public TimeSpan NextAttack;

    [DataField]
    public float FireTrailInterval = 0.6f;
    [ViewVariables, AutoPausedField]
    public TimeSpan NextFireTrail;

    /// <summary>Цель, найденная последним поиском (поиск по области — не чаще раза в полсекунды).</summary>
    [ViewVariables]
    public EntityUid? CachedTarget;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextRetarget;
}
