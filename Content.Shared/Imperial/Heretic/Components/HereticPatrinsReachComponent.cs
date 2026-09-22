using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticPatrinsReachComponent : Component
{
    public EntityUid CasterUid;
    public Vector2 TargetWorldPos;
    public float Speed = 3f;
    public float MaxRange = 20f;
    public float TraveledDistance = 0f;
    public float RustChance = 0.75f;
    public Vector2i? LastTile;
}
