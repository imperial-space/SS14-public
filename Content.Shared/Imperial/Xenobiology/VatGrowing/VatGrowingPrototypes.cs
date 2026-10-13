using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.VatGrowing;

/// <summary>
/// datum/micro_organism/cell_line: клеточная линия, растущая в чане.
/// </summary>
[Prototype]
public sealed partial class CellLinePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>desc.</summary>
    [DataField(required: true)]
    public LocId Desc;

    /// <summary>required_reagents: без них линия не растёт и каждый тик их съедает.</summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> Required = new();

    /// <summary>supplementary_reagents: ускоряют рост.</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> Supplementary = new();

    /// <summary>suppressive_reagents: замедляют рост (значения отрицательные).</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> Suppressive = new();

    /// <summary>virus_suspectibility: на сколько вирус замедляет рост.</summary>
    [DataField]
    public float VirusSuspectibility = 1f;

    /// <summary>growth_rate (VAT_GROWTH_RATE = 4).</summary>
    [DataField]
    public float GrowthRate = 4f;

    /// <summary>resulting_atom (если вариантов несколько — случайный).</summary>
    [DataField(required: true)]
    public List<EntProtoId> Results = new();

    /// <summary>resulting_atom_count.</summary>
    [DataField]
    public int ResultCount = 1;
}

/// <summary>
/// GLOB.cell_line_tables: взвешенный набор клеточных линий одного источника.
/// </summary>
[Prototype]
public sealed partial class CellLineTablePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<ProtoId<CellLinePrototype>, float> Lines = new();
}

/// <summary>
/// element/swabable: что можно взять мазком и какую пробу это даст.
/// Источник определяется по прототипу сущности (или его родителям) либо по реагенту лужи.
/// </summary>
[Prototype]
public sealed partial class SwabSourcePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Прототипы (и наследники), у которых можно взять пробу.</summary>
    [DataField]
    public List<string> Entities = new();

    /// <summary>Лужи, содержащие один из реагентов (кровь, рвота).</summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> PuddleReagents = new();

    [DataField(required: true)]
    public ProtoId<CellLineTablePrototype> Table;

    /// <summary>cell_line_amount: случайно от Min до Max.</summary>
    [DataField]
    public int MinAmount = 1;

    [DataField]
    public int MaxAmount = 1;

    /// <summary>virus_chance, проценты.</summary>
    [DataField]
    public float VirusChance = 5f;

    /// <summary>Источник — живое существо (берётся биопсией, а не тампоном).</summary>
    [DataField]
    public bool Mob;
}

/// <summary>Цвет пробы (GLOB.xeno_sample_colors) и состояние чана для визуализаторов.</summary>
[Robust.Shared.Serialization.NetSerializable, Serializable]
public enum VatGrowingVisuals : byte
{
    /// <summary>Строка: brown, gray, green, purple, yellow или none.</summary>
    SampleColor,
    /// <summary>В чане есть жидкость.</summary>
    HasReagents,
    /// <summary>Ресемплер включён.</summary>
    Resampler,
    /// <summary>В чане растёт проба.</summary>
    Growing,
}
