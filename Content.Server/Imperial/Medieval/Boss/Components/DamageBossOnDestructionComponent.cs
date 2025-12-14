namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class DamageBossOnDestructionComponent : Component
{
    [DataField]
    public float DamageAmount = 10f;
}
