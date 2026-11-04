using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Карта-кабина машины.</summary>
[RegisterComponent]
public sealed partial class CadiaVehicleInteriorComponent : Component
{
    [ViewVariables]
    public EntityUid? Vehicle;
}
