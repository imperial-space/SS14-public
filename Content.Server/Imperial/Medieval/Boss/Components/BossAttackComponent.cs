namespace Content.Server.Imperial.Medieval.Boss;

/// <summary>
/// Компонент, используемый для атаки босса, позволяет связывать босса с его атаками.
/// </summary>
[RegisterComponent]
public sealed partial class BossAttackComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid Boss;
}
