using System.Numerics;
using Content.Shared.Coordinates;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.DarkMage.Follower;

public sealed partial class FollowerSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MedievalFollowerComponent>();
        while (query.MoveNext(out var uid, out var follower))
        {
            _transformSystem.SetCoordinates(uid, follower.Target.ToCoordinates().Offset(new Vector2(follower.X, follower.Y)));
        }
    }
}
[RegisterComponent]
public sealed partial class MedievalFollowerComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid Target;
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float X = 0f;
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float Y = 0.3f;
}
