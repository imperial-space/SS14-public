using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Снять с пациента мутацию, которой нет в его геноме (nullify).</summary>
[Serializable, NetSerializable]
public sealed class DnaConsoleNullifyMessage(ProtoId<GeneticMutationPrototype> mutation) : BoundUserInterfaceMessage
{
    public readonly ProtoId<GeneticMutationPrototype> Mutation = mutation;
}
