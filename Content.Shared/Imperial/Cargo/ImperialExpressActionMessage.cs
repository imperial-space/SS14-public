using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

[Serializable, NetSerializable]
public sealed class ImperialExpressActionMessage(ImperialExpressAction action) : BoundUserInterfaceMessage
{
    public readonly ImperialExpressAction Action = action;
}
