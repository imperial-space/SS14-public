using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Казённик основного орудия: в него вкладывают снаряд.</summary>
[RegisterComponent]
public sealed partial class CadiaVehicleBreechComponent : Component
{
    [ViewVariables]
    public EntityUid? Vehicle;
}
