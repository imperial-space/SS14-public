using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

[Serializable, NetSerializable]
public sealed class ImperialExpressBuyMessage(string pack) : BoundUserInterfaceMessage
{
    public readonly string Pack = pack;
}
