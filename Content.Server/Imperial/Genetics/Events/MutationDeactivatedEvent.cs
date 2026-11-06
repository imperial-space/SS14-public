using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Genetics;

/// <summary>Мутация снята с носителя.</summary>
[ByRefEvent]
public readonly record struct MutationDeactivatedEvent(ProtoId<GeneticMutationPrototype> Mutation);
