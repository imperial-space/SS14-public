using Content.Shared.Damage;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

[RegisterComponent]
public sealed partial class DeathNoteMimicComponent : Component
{
    [ViewVariables]
    public EntityUid Target;

    [ViewVariables]
    public DamageSpecifier MinimumTargetDamage = new();

    [ViewVariables]
    public DamageSpecifier MaximumTargetDamage = new();

    /// <summary>
    /// Настроенный суммарный урон распределяется по указанному числу успешных ударов по цели.
    /// </summary>
    [ViewVariables]
    public int TargetHitsRemaining;

    [ViewVariables]
    public DamageSpecifier TargetDamageRemaining = new();

    [ViewVariables]
    public TimeSpan NextRetargetAt;

    [ViewVariables]
    public TimeSpan RetargetInterval;
}
