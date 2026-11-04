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
