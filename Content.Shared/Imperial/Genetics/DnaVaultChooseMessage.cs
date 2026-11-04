using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Выбрать улучшение из своей пары.</summary>
[Serializable, NetSerializable]
public sealed class DnaVaultChooseMessage(ProtoId<GeneticMutationPrototype> mutation) : BoundUserInterfaceMessage
{
    public readonly ProtoId<GeneticMutationPrototype> Mutation = mutation;
}
