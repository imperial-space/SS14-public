using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics.Components;

[DataDefinition]
public sealed partial class GeneBlock
{
    [DataField(required: true)]
    public ProtoId<GeneticMutationPrototype> Mutation;

    [DataField]
    public string Sequence = string.Empty;

    /// <summary>Исходная (сколотая) последовательность гена (default_mutation_genes): к ней возвращает мутадон.</summary>
    [DataField]
    public string Default = string.Empty;
}
