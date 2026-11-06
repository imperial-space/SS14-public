using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModularShield;

[Serializable, NetSerializable]
public enum ImperialModularShieldWireStatus : byte
{
    Key,
}
