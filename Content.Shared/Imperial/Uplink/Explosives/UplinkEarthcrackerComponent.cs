using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Explosives;

/// <summary>Землеразлом E-2.</summary>
[RegisterComponent]
public sealed partial class UplinkEarthcrackerComponent : Component
{
    [ViewVariables]
    public UplinkEarthcrackerStatus Status = UplinkEarthcrackerStatus.Ready;

    [ViewVariables]
    public TimeSpan? ActivateAt;
}
