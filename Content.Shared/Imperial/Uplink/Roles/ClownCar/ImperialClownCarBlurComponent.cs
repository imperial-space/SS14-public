using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles.ClownCar;

/// <summary>set_eye_blur_if_lower после удара об оленя.</summary>
[RegisterComponent]
public sealed partial class ImperialClownCarBlurComponent : Component
{
    [ViewVariables]
    public TimeSpan EndAt;
}
