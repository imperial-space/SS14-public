namespace Content.Shared.Imperial.Heretic.Items;

[RegisterComponent]
public sealed partial class HereticEldritchCardComponent : Component
{
    /// <summary>
    /// The first portal placed (waiting for a second door to link to).
    /// </summary>
    public EntityUid? PendingPortal;
}
