using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public sealed class CadiaBatterySetTargetMessage(Vector2 target) : BoundUserInterfaceMessage
{
    public readonly Vector2 Target = target;
}
