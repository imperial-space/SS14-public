using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.VatGrowing;

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
