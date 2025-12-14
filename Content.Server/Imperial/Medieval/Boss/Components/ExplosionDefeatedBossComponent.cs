namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class ExplosionDefeatedBossComponent : Component
{
    [DataField]
    public int Explosions = 5;

    [DataField]
    public float Delay = 1f;

    public int Index = 0;

    public TimeSpan NextExplosion = TimeSpan.Zero;
}
