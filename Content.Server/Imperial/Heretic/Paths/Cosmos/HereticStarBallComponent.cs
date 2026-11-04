using System.Numerics;

namespace Content.Server.Imperial.Heretic.Paths.Cosmos;

[RegisterComponent]
public sealed partial class HereticStarBallComponent : Component
{
    [ViewVariables]
    public EntityUid CasterUid;
    [ViewVariables]
    public Vector2 TargetWorldPos;
    // ~2.5 tiles/sec — SS13 speed=0.2 at ~20 ticks/sec ≈ 4 tiles/sec, adjusted for SS14 feel
    [ViewVariables]
    public float Speed = 2.5f;
    // SS13 range=25
    [ViewVariables]
    public float MaxRange = 25f;
    [ViewVariables]
    public float TraveledDistance = 0f;
}
