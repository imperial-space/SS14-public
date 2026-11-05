namespace Content.Server.Imperial.Heretic.Effects;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class HereticAmokComponent : Component
{
    [DataField]
    public float Duration = 10f;

    [DataField]
    public float AttackCooldown = 1.5f;

    [ViewVariables, AutoPausedField]
    public TimeSpan End;
    [ViewVariables, AutoPausedField]
    public TimeSpan NextAttack;
}
