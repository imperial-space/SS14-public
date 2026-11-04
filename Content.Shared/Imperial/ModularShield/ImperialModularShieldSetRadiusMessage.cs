using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModularShield;

[Serializable, NetSerializable]
public sealed class ImperialModularShieldSetRadiusMessage(float radius) : BoundUserInterfaceMessage
{
    public readonly float Radius = radius;
}
