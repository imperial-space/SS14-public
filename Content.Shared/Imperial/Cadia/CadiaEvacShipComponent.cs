using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Транспорт спускается на площадку и улетает: сервер считает время, рисует клиент.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaEvacShipComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public TimeSpan LandAt;

    [DataField, AutoNetworkedField]
    public TimeSpan LeaveAt;

    [DataField, AutoNetworkedField]
    public TimeSpan GoneAt;
}
