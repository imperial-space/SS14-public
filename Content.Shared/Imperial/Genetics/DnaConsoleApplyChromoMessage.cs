using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

[Serializable, NetSerializable]
public sealed class DnaConsoleApplyChromoMessage(DnaMutationRef mutation, ChromosomeKind kind) : BoundUserInterfaceMessage
{
    public readonly DnaMutationRef Mutation = mutation;
    public readonly ChromosomeKind Kind = kind;
}
