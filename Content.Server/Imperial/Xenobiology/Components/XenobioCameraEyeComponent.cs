using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>mob/eye/camera/remote/xenobio.</summary>
[RegisterComponent]
public sealed partial class XenobioCameraEyeComponent : Component
{
    [ViewVariables]
    public EntityUid Console;

    [ViewVariables]
    public EntityUid Mind;

    [ViewVariables]
    public EntityUid Body;

    [ViewVariables]
    public EntityCoordinates LastValid;
}
