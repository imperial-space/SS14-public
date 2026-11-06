namespace Content.Server.Imperial.Heretic.Paths.Moon;

[RegisterComponent]
public sealed partial class HereticMoonIllusionMovementComponent : Component
{
    [DataField]
    public float Speed = 1.5f;

    [ViewVariables]
    public TimeSpan NextDirectionChange;
    [ViewVariables]
    public TimeSpan NextFootstep;
    [ViewVariables]
    public float CurrentAngle;

    [DataField]
    public TimeSpan FootstepDelay = TimeSpan.FromSeconds(0.5f);
}
