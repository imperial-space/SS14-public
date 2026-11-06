using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics.Components;

/// <summary>Сколько инфузий каждого набора получило существо.</summary>
[RegisterComponent]
public sealed partial class GeneticInfusionsComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<GeneticInfusionPrototype>, int> Counts = new();
}
