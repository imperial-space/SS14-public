using System.Numerics;

namespace Content.Shared.Imperial.Heretic.Paths.Lock;

[RegisterComponent]
public sealed partial class HereticPatrinsReachComponent : Component
{
    [ViewVariables]
    public EntityUid CasterUid;
    [ViewVariables]
    public Vector2 TargetWorldPos;
    [ViewVariables]
    public float Speed = 3f;
    [ViewVariables]
    public float MaxRange = 20f;
    [ViewVariables]
    public float TraveledDistance = 0f;
    [ViewVariables]
    public float RustChance = 0.75f;
    [ViewVariables]
    public Vector2i? LastTile;
}
