using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research;

/// <summary>researchNode: изучить узел с учётом скидочных экспериментов.</summary>
[Serializable, NetSerializable]
public sealed class ImperialTgResearchNodeMessage : BoundUserInterfaceMessage
{
    public readonly string Node;

    public ImperialTgResearchNodeMessage(string node)
    {
        Node = node;
    }
}
