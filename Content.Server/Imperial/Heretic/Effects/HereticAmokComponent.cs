namespace Content.Server.Imperial.Heretic.Effects;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class HereticAmokComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10f);

    [DataField]
    public TimeSpan AttackCooldown = TimeSpan.FromSeconds(1.5f);

    [ViewVariables, AutoPausedField]
    public TimeSpan End;
    [ViewVariables, AutoPausedField]
    public TimeSpan NextAttack;
}
