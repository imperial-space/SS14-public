using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Magic.ProjectileCursorFollower;


[RegisterComponent, NetworkedComponent]
public sealed partial class ProjectileCursorFollowerComponent : Component
{
    [DataField]
    public float LinearVelocityIntensy = 1.0f;

    [DataField]
    public float MaxLinearVelocityIntensy = 15.0f;
    [DataField]
    public Angle RelativeAngle = Angle.Zero;
}
