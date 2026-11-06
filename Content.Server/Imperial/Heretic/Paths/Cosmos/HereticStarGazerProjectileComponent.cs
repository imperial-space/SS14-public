namespace Content.Server.Imperial.Heretic.Paths.Cosmos;

/// <summary>Маркер для снарядов Star Gazer, чтобы не конфликтовать с HereticStarBlastProjectileComponent.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class HereticStarGazerProjectileComponent : Component
{
    [DataField] public EntityUid? Shooter;
    [DataField]
    [ViewVariables, AutoPausedField]
    public TimeSpan NextTrail;
    [DataField] public TimeSpan TrailInterval = TimeSpan.FromSeconds(0.3f);

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
