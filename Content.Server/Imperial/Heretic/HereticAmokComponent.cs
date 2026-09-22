namespace Content.Server.Imperial.Heretic;

[RegisterComponent]
public sealed partial class HereticAmokComponent : Component
{
    [DataField]
    public float Duration = 10f;

    [DataField]
    public float AttackCooldown = 1.5f;

    public float Elapsed;
    public float AttackTimer;
}
