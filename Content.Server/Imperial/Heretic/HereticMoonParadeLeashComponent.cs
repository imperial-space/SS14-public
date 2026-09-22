namespace Content.Server.Imperial.Heretic;

[RegisterComponent]
public sealed partial class HereticMoonParadeLeashComponent : Component
{
    public EntityUid Projectile;
    public float TimeRemaining = 20f;
    public float DamageAccumulated = 0f;

    public const float DamageThreshold = 50f;
    public const float PullSpeed = 3f;
}
