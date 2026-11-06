using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

[Serializable, NetSerializable]
public sealed class DnaAdvancedInjectorState
{
    public string Name = string.Empty;
    public List<DnaMutationState> Mutations = new();
}
