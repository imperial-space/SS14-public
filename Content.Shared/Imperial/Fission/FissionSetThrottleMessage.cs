using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

[Serializable, NetSerializable]
public sealed class FissionSetThrottleMessage(float throttle) : BoundUserInterfaceMessage
{
    public readonly float Throttle = throttle;
}
