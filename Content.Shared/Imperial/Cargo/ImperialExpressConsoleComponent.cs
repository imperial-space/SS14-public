using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

/// <summary>/obj/machinery/computer/cargo/express.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialExpressConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? Beacon;

    [DataField]
    public bool Locked = true;

    [ViewVariables]
    public bool UsingBeacon;

    /// <summary>pod_type: блюспейс-капсулы после диска улучшения.</summary>
    [DataField]
    public bool BluespacePods;

    [ViewVariables]
    public TimeSpan NextBeaconPrint;

    [ViewVariables]
    public TimeSpan NextLaunch;
}
