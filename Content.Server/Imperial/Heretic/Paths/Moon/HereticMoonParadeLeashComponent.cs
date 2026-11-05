namespace Content.Server.Imperial.Heretic.Paths.Moon;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class HereticMoonParadeLeashComponent : Component
{
    [ViewVariables]
    public EntityUid Projectile;
    [ViewVariables, AutoPausedField]
    public TimeSpan End;
    [ViewVariables]
    public float DamageAccumulated = 0f;

    public const float DamageThreshold = 50f;
    public const float PullSpeed = 3f;
}
