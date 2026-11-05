using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

[Serializable, NetSerializable]
public sealed class DnaConsoleCombineMessage(DnaMutationRef first, DnaMutationRef second) : BoundUserInterfaceMessage
{
    public readonly DnaMutationRef First = first;
    public readonly DnaMutationRef Second = second;
}
