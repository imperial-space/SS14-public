namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

[RegisterComponent]
public sealed partial class DeathNoteMimicComponent : Component
{
    [ViewVariables]
    public EntityUid Target;

    [ViewVariables]
    public float MinimumTargetDamage;

    [ViewVariables]
    public float MaximumTargetDamage;

    /// <summary>
    /// Настроенный суммарный урон распределяется по указанному числу успешных ударов по цели.
    /// </summary>
    [ViewVariables]
    public int TargetHitsRemaining;

    [ViewVariables]
    public float TargetDamageRemaining;

    [ViewVariables]
    public TimeSpan NextRetargetAt;

    [ViewVariables]
    public TimeSpan RetargetInterval;
}
