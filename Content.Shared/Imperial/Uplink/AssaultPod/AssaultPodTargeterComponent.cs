using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.AssaultPod;

/// <summary>
/// Устройство наведения штурмового пода (Paradise, /obj/item/assault_pod):
/// выбирает маяк станции и разворачивает десантную капсулу.
/// </summary>
[RegisterComponent]
public sealed partial class AssaultPodTargeterComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Pod;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public string? TargetName;
}
