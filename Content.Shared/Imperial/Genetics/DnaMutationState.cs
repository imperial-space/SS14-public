using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Мутация в окне консоли.</summary>
[Serializable, NetSerializable]
public sealed class DnaMutationState
{
    public DnaMutationRef Ref;
    public ProtoId<GeneticMutationPrototype> Mutation;
    public string Alias = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public MutationQuality Quality;
    public int Instability;
    public bool Discovered;
    public bool Active;
    public DnaMutationClass Class;
    public string Sequence = string.Empty;
    public string DefaultSequence = string.Empty;
    public DnaChromosomeState CanChromo;
    public ChromosomeKind AppliedChromo;
    public ChromosomeKind ValidChromos;
}
