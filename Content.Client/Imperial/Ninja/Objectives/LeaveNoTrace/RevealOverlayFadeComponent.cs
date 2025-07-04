using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Client.Imperial.LeaveNoTrace;


[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class RevealOverlayFadeComponent : Component
{
    /// <summary>
    /// A time after we remove <see cref="RevealOverlay" />
    /// </summary>
    [DataField]
    public TimeSpan RemoveRevealOverlayTime = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The exact time we remove the component
    /// </summary>
    [DataField(readOnly: true, customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan RemoveRevealOverlayEndTime = TimeSpan.Zero;
}
