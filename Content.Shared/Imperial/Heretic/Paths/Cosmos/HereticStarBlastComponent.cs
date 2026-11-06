namespace Content.Shared.Imperial.Heretic.Paths.Cosmos;

[RegisterComponent]
public sealed partial class HereticStarBlastComponent : Component
{
    [DataField]
    public EntityUid? ActiveProjectile;

    [ViewVariables]
    public EntityUid? StarBlastAction;

    [DataField]
    public TimeSpan ShootDelay = TimeSpan.FromSeconds(0.5);
}
