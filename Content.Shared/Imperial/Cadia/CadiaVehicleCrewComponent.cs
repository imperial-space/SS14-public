using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Боец внутри машины: где сидит. По нему клиент рисует табло экипажа и прицел.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaVehicleCrewComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Vehicle;

    [DataField, AutoNetworkedField]
    public CadiaVehicleSeat Seat;

    [ViewVariables]
    public List<EntityUid> Actions = new();
}
