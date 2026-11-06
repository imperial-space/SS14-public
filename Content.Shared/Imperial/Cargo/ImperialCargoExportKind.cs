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
