using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Изменить позицию гена пациента. С «Джокером» позиция открывается верной.</summary>
[Serializable, NetSerializable]
public sealed class DnaConsolePulseGeneMessage(ProtoId<GeneticMutationPrototype> mutation, int position, DnaGeneAction action, bool joker)
    : BoundUserInterfaceMessage
{
    public readonly ProtoId<GeneticMutationPrototype> Mutation = mutation;
    public readonly int Position = position;
    public readonly DnaGeneAction Action = action;
    public readonly bool Joker = joker;
}
