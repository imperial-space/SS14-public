using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Shared.Imperial.Heretic.Paths.Ash;

[RegisterComponent, AutoGenerateComponentPause, NetworkedComponent]
public sealed partial class HereticAshManComponent : Component
{
    [ViewVariables]
    public EntityUid? CasterUid;

    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromSeconds(60f);
    [ViewVariables, AutoPausedField]
    public TimeSpan LifetimeEnd;

    [DataField]
    public float MoveSpeed = 4.5f;

    [DataField]
    public float AttackRange = 1.3f;

    [DataField]
    public TimeSpan AttackCooldown = TimeSpan.FromSeconds(1.2f);
    [ViewVariables, AutoPausedField]
    public TimeSpan NextAttack;

    [DataField]
    public TimeSpan FireTrailInterval = TimeSpan.FromSeconds(0.6f);
    [ViewVariables, AutoPausedField]
    public TimeSpan NextFireTrail;

    /// <summary>Цель, найденная последним поиском (поиск по области — не чаще раза в полсекунды).</summary>
    [ViewVariables]
    public EntityUid? CachedTarget;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextRetarget;

    [DataField]
    public DamageSpecifier UpdateDamage = new() { DamageDict = { ["Slash"] = 10, ["Heat"] = 8, ["Blunt"] = 5 } };
}
