using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

[Serializable, NetSerializable]
public sealed class ImperialPrivateOrderBuyMessage(string pack) : BoundUserInterfaceMessage
{
    public readonly string Pack = pack;
}
