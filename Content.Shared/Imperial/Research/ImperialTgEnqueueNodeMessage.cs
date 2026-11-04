using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research;

[Serializable, NetSerializable]
public sealed class ImperialTgEnqueueNodeMessage : BoundUserInterfaceMessage
{
    public readonly string Node;

    public ImperialTgEnqueueNodeMessage(string node)
    {
        Node = node;
    }
}
