using Robust.Shared.Serialization.Manager.Attributes;

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
    public float TrailInterval = 0.3f;
}
