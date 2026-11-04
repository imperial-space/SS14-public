using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>capturedevice: хранит одно простое существо.</summary>
[RegisterComponent]
public sealed partial class SlimeCaptureDeviceComponent : Component
{
    [DataField]
    public string Container = "capture";
}
