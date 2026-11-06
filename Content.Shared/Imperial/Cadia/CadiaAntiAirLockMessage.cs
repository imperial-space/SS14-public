using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Захватить точку на корпусе просматриваемого борта (координаты клика в мире).</summary>
[Serializable, NetSerializable]
public sealed class CadiaAntiAirLockMessage(NetCoordinates point) : BoundUserInterfaceMessage
{
    public readonly NetCoordinates Point = point;
}
