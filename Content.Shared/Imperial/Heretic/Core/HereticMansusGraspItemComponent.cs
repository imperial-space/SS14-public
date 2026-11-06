namespace Content.Shared.Imperial.Heretic.Core;

/// <summary>
/// Marks the spawned Mansus Grasp spell item. Used like the cult stun item — applied to a target, then consumed.
/// </summary>
[RegisterComponent]
public sealed partial class HereticMansusGraspItemComponent : Component
{
    [DataField]
    public TimeSpan MansusGraspItemAfterInteractDuration = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan MansusGraspItemAfterInteractTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan MansusGraspItemAfterInteractTime2 = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan MansusGraspItemAfterInteractCooldown = TimeSpan.FromSeconds(10);
}
