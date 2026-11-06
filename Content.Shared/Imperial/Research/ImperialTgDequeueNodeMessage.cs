using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research;

[Serializable, NetSerializable]
public sealed class ImperialTgDequeueNodeMessage : BoundUserInterfaceMessage
{
    public readonly string Node;

    public ImperialTgDequeueNodeMessage(string node)
    {
        Node = node;
    }
}
