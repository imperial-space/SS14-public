using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Мутация включилась у носителя.</summary>
[ByRefEvent]
public readonly record struct MutationActivatedEvent(ProtoId<GeneticMutationPrototype> Mutation, ChromosomeKind Chromosome);
