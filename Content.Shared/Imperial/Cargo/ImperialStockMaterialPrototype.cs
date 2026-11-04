using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cargo;

/// <summary>Материал биржи SSstock_market (/datum/material с tradable = TRUE).</summary>
[Prototype]
public sealed partial class ImperialStockMaterialPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>value_per_unit × SHEET_MATERIAL_AMOUNT: базовая цена листа.</summary>
    [DataField(required: true)]
    public float Value;

    /// <summary>minimum_value_override × SHEET_MATERIAL_AMOUNT.</summary>
    [DataField]
    public float? Minimum;

    /// <summary>tradable_base_quantity.</summary>
    [DataField(required: true)]
    public int BaseQuantity;

    /// <summary>Лист этого материала для покупки на рынке.</summary>
    [DataField]
    public EntProtoId? Sheet;
}
