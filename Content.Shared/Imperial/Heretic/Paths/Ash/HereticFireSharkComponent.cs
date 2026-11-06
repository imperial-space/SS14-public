using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Shared.Imperial.Heretic.Paths.Ash;

[RegisterComponent, AutoGenerateComponentPause, NetworkedComponent]
public sealed partial class HereticFireSharkComponent : Component
{
    [ViewVariables]
    public EntityUid? TargetUid;

    [DataField] public TimeSpan Lifetime = TimeSpan.FromSeconds(25f);
    [ViewVariables, AutoPausedField]
    public TimeSpan LifetimeEnd;

    [DataField] public float MoveSpeed = 7f;
    [DataField] public float AttackRange = 1.3f;
    [DataField] public TimeSpan AttackCooldown = TimeSpan.FromSeconds(0.6f);
    [ViewVariables, AutoPausedField]
    public TimeSpan NextAttack;

    [DataField] public TimeSpan FireTrailInterval = TimeSpan.FromSeconds(0.35f);
    [ViewVariables, AutoPausedField]
    public TimeSpan NextFireTrail;

    [DataField] public TimeSpan RegenInterval = TimeSpan.FromSeconds(1f);
    [DataField] public float RegenAmount = 1f;
    [ViewVariables, AutoPausedField]
    public TimeSpan NextRegen;

    /// <summary>Цель, найденная последним поиском (поиск по области — не чаще раза в полсекунды).</summary>
    [ViewVariables]
    public EntityUid? CachedTarget;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextRetarget;

    [DataField]
    public DamageSpecifier UpdateBluntDamage = new() { DamageDict = { ["Blunt"] = 8 } };

    [DataField]
    public DamageSpecifier BonusDamage = new() { DamageDict = { ["Heat"] = 25 } };

    [DataField]
    public float ResolveTargetRange = 15f;
}
