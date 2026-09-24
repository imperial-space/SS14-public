using System.Numerics;

namespace Content.Server.Imperial.Heretic;

[RegisterComponent]
public sealed partial class HereticStarBallComponent : Component
{
    public EntityUid CasterUid;
    public Vector2 TargetWorldPos;
    // ~2.5 tiles/sec — SS13 speed=0.2 at ~20 ticks/sec ≈ 4 tiles/sec, adjusted for SS14 feel
    public float Speed = 2.5f;
    // SS13 range=25
    public float MaxRange = 25f;
    public float TraveledDistance = 0f;
}
