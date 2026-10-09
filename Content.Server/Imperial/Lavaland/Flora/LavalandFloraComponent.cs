using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.Flora;

/// <summary>
/// Флора лаваленда (structure/flora/ash и flora/rock из SS13): собирается руками или киркой,
/// растения отрастают, камни исчезают.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandFloraComponent : Component
{
    /// <summary>base_icon_state: итоговое состояние — BaseState + номер варианта.</summary>
    [DataField(required: true)] public string BaseState = string.Empty;

    /// <summary>number_of_variants; 0 — состояние без номера.</summary>
    [DataField] public int Variants = 4;

    /// <summary>Разделитель между BaseState и номером (volcano_1).</summary>
    [DataField] public string VariantSeparator = string.Empty;

    /// <summary>Веса вариантов (pick(3;1,3;2,1;3)); пусто — равновероятно.</summary>
    [DataField] public List<float> VariantWeights = new();

    /// <summary>Светящийся второй слой: состояние варианта + суффикс, пока не собрано.</summary>
    [DataField] public string? EmissiveSuffix;

    /// <summary>get_potential_products: прототип и вес.</summary>
    [DataField(required: true)] public Dictionary<EntProtoId, float> Products = new();

    [DataField] public int AmountLow = 1;
    [DataField] public int AmountHigh = 3;
    [DataField] public float HarvestTime = 6f;

    /// <summary>Нужен инструмент с этим тегом (кирка для камней); null — руками.</summary>
    [DataField] public ProtoId<TagPrototype>? ToolTag;

    /// <summary>delete_on_harvest: камни исчезают, растения отрастают.</summary>
    [DataField] public bool DeleteOnHarvest;

    [DataField] public float RegrowthLow = 480f;
    [DataField] public float RegrowthHigh = 960f;

    /// <summary>Свет после сбора (fireblossom тускнеет, glowgrowth гаснет).</summary>
    [DataField] public float? HarvestedLightRadius;

    /// <summary>Вулканическая пора после добычи превращается в лаву.</summary>
    [DataField] public EntProtoId? SpawnOnHarvest;

    [DataField] public LocId? HarvestedName;
    [DataField] public LocId? HarvestedDesc;
    [DataField] public LocId HarvestMessage = "lavaland-flora-harvest";

    [ViewVariables] public int Variant = 1;
    [ViewVariables] public bool Harvested;
    [ViewVariables] public TimeSpan RegrowAt;
    [ViewVariables] public float OriginalLightRadius;
    [ViewVariables] public string OriginalName = string.Empty;
    [ViewVariables] public string OriginalDesc = string.Empty;
}
