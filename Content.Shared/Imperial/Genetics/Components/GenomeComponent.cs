using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics.Components;

/// <summary>
/// Геном гуманоида (datum/dna из SS13): блоки со спящими генами мутаций и активные мутации.
/// Заводится при первом обращении генетики к существу.
/// </summary>
[RegisterComponent]
public sealed partial class GenomeComponent : Component
{
    /// <summary>Блоки генов (mutation_index): мутация и её текущая последовательность, «X» — неизвестная позиция.</summary>
    [DataField]
    public List<GeneBlock> Blocks = new();

    [DataField]
    public Dictionary<ProtoId<GeneticMutationPrototype>, ActiveMutation> Active = new();

    /// <summary>Генетическая стабильность. При 0 и ниже начинается распад ДНК.</summary>
    [DataField]
    public int Stability = 100;

    /// <summary>Когда случится срыв, если стабильность не восстановить.</summary>
    [DataField]
    public TimeSpan? MeltdownAt;

    /// <summary>Генетический урон (status_effect/genetic_damage): копится от работы консоли, спадает со временем.</summary>
    [DataField]
    public float GeneticDamage;
}
