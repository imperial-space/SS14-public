using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics.Prototypes;

/// <summary>
/// Рецепт комбинирования двух мутаций в третью (datum/generecipe из SS13).
/// </summary>
[Prototype]
public sealed partial class GeneticRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<GeneticMutationPrototype> First;

    [DataField(required: true)]
    public ProtoId<GeneticMutationPrototype> Second;

    [DataField(required: true)]
    public ProtoId<GeneticMutationPrototype> Result;
}
