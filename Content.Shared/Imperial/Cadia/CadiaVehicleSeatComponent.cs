using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Кресло в кабине: пристегнулся — занял место водителя или наводчика, камера уходит наружу.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaVehicleSeatComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    [DataField, AutoNetworkedField]
    public CadiaVehicleSeat Seat;
}
