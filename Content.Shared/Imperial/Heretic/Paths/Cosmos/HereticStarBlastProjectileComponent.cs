namespace Content.Shared.Imperial.Heretic.Paths.Cosmos;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class HereticStarBlastProjectileComponent : Component
{
    [DataField]
    public EntityUid? Shooter;

    [DataField]
    public float Speed = 2f;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextTrail;

    [DataField]
    public TimeSpan TrailInterval = TimeSpan.FromSeconds(0.3f);

    [DataField]
    public TimeSpan ProjectileCollideMobSensorTime = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan ProjectileCollideMobSensorDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan ProjectileTerminatingDelay = TimeSpan.FromSeconds(25);

    [DataField]
    public TimeSpan ProjectileTerminatingCooldown = TimeSpan.FromSeconds(25);

    [DataField]
    public float ProjectileCollideMobSensorRange = 3f;
}
