using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.Imperial.Heretic.Paths.Moon;

[RegisterComponent]
public sealed partial class HereticMoonIllusionMovementComponent : Component
{
    [DataField]
    public float Speed = 1.5f;

    public TimeSpan NextDirectionChange;
    public TimeSpan NextFootstep;
    public float CurrentAngle;
}
