using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Спуск нажат или отпущен.</summary>
[Serializable, NetSerializable]
public sealed class CadiaVehicleFireEvent(NetEntity vehicle, bool down) : EntityEventArgs
{
    public readonly NetEntity Vehicle = vehicle;
    public readonly bool Down = down;
}
