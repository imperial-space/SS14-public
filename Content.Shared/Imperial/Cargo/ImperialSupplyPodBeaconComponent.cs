using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

/// <summary>/obj/item/supplypod_beacon.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialSupplyPodBeaconComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Console;

    [ViewVariables]
    public bool Ready;

    [ViewVariables]
    public TimeSpan? LaunchedUntil;
}
