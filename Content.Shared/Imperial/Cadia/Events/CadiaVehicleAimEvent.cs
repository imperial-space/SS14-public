using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Наводчик ведёт башню за курсором.</summary>
[Serializable, NetSerializable]
public sealed class CadiaVehicleAimEvent(NetEntity vehicle, NetCoordinates target) : EntityEventArgs
{
    public readonly NetEntity Vehicle = vehicle;
    public readonly NetCoordinates Target = target;
}
