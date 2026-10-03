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

[DataDefinition]
public sealed partial class ActiveMutation
{
    [DataField]
    public MutationSource Sources;

    [DataField]
    public ChromosomeKind Chromosome = ChromosomeKind.None;

    /// <summary>Компоненты, которые добавила мутация (их и снимаем).</summary>
    [DataField]
    public List<string> AddedComponents = new();

    [ViewVariables]
    public List<EntityUid> ActionEntities = new();
}

[Flags]
public enum MutationSource : byte
{
    None = 0,

    /// <summary>Ген активирован в собственном геноме (секвенсор, активатор).</summary>
    Activated = 1 << 0,

    /// <summary>Добавлена мутатором поверх генома — тратит стабильность.</summary>
    Mutator = 1 << 1,

    Admin = 1 << 2,

    /// <summary>Награда ДНК-хранилища: не снимается мутадоном, срывом и перемешиванием.</summary>
    Vault = 1 << 3,
}
