using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.AssaultPod;

/// <summary>
/// Метка зоны посадки пода (как pod_landingzone у капсул снабжения TG): поворачивается и сжимается к моменту касания.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AssaultPodLandingMarkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public TimeSpan Arrival;
}
