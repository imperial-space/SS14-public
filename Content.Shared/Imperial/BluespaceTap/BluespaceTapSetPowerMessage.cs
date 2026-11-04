using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.BluespaceTap;

[Serializable, NetSerializable]
public sealed class BluespaceTapSetPowerMessage(float power) : BoundUserInterfaceMessage
{
    public readonly float Power = power;
}
