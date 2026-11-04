namespace Content.Server.Imperial.Heretic.Paths.Moon;

[RegisterComponent]
public sealed partial class HereticMoonParadeLeashComponent : Component
{
    [ViewVariables]
    public EntityUid Projectile;
    [ViewVariables]
    public float TimeRemaining = 20f;
    [ViewVariables]
    public float DamageAccumulated = 0f;

    public const float DamageThreshold = 50f;
    public const float PullSpeed = 3f;
}
