using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public sealed class CadiaAntiAirAimMessage(Vector2 offset) : BoundUserInterfaceMessage
{
    public readonly Vector2 Offset = offset;
}
