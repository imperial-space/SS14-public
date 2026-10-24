using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cargo;

/// <summary>Как считается количество и цена экспорта (get_amount/get_base_cost у /datum/export/*).</summary>
public enum ImperialCargoExportKind : byte
{
    /// <summary>Штука.</summary>
    Item,

    /// <summary>/datum/export/stack: количество — размер стопки.</summary>
    Stack,

    /// <summary>/datum/export/material: листы материала в стопке из одного материала, руда ×0.8.</summary>
    Material,

    /// <summary>/datum/export/food: любая твёрдая еда, кроме урожая.</summary>
    Food,

    /// <summary>/datum/export/seed и seed/potency.</summary>
    Seed,

    /// <summary>/datum/export/gas_canister: ценные газы внутри.</summary>
    GasCanister,

    /// <summary>/datum/export/reagent_dispenser: доля заполнения бака.</summary>
    ReagentTank,
}

/// <summary>/datum/export: что и почём покупает ЦК у шаттла снабжения.</summary>
[Prototype]
public sealed partial class ImperialCargoExportPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Порядок проверки (GLOB.exports_list): срабатывает первый подходящий экспорт.</summary>
    [DataField]
    public int Priority;

    [DataField]
    public ImperialCargoExportKind Kind = ImperialCargoExportKind.Item;

    /// <summary>unit_name.</summary>
    [DataField]
    public LocId Unit = string.Empty;

    /// <summary>cost за единицу.</summary>
    [DataField]
    public float Cost = 1;

    /// <summary>Начальная k_elasticity: 0 — цена не падает от продаж.</summary>
    [DataField]
    public float Elasticity = 1;

    /// <summary>k_hit_percentile.</summary>
    [DataField]
    public float Hit = 0.05f;

    /// <summary>k_recovery_time, секунды.</summary>
    [DataField]
    public float Recovery = 600;

    /// <summary>export_types.</summary>
    [DataField]
    public List<EntProtoId> Entities = new();

    /// <summary>include_subtypes: подходят и наследники.</summary>
    [DataField]
    public bool IncludeChildren = true;

    /// <summary>exclude_types.</summary>
    [DataField]
    public List<EntProtoId> Exclude = new();

    /// <summary>material_id: материалы SS14, листы которых продаются этим экспортом.</summary>
    [DataField]
    public List<ProtoId<MaterialPrototype>> Materials = new();

    /// <summary>Рыночный материал: цена берётся с биржи (/datum/export/material/market).</summary>
    [DataField]
    public ProtoId<ImperialStockMaterialPrototype>? Market;

    /// <summary>contents_cost у баков; для семян — цена улучшенного образца за единицу потенции.</summary>
    [DataField]
    public float ExtraCost;
}

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
