using System.Numerics;
using Content.Shared.Damage;

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

    [DataField]
    public DamageSpecifier HitDamage = new() { DamageDict = { ["Heat"] = 20 } };

    [DataField]
    public TimeSpan BallArriveDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan BallArriveTime = TimeSpan.FromSeconds(4);

    [DataField]
    public float BallArriveRange = 3f;
}
