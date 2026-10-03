using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics.Components;

/// <summary>ДНК-инфузер (dna_infuser из SS13).</summary>
[RegisterComponent]
public sealed partial class DnaInfuserComponent : Component
{
    /// <summary>Радиус поиска существа-источника рядом с машиной.</summary>
    [DataField]
    public float SourceRange = 1.6f;

    [DataField]
    public TimeSpan InfuseTime = TimeSpan.FromSeconds(8);
}

/// <summary>Сколько инфузий каждого набора получило существо.</summary>
[RegisterComponent]
public sealed partial class GeneticInfusionsComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<GeneticInfusionPrototype>, int> Counts = new();
}
