using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Fishing;

/// <summary>
/// Источник рыбы (/datum/fish_source): таблица улова, сложность, фон мини-игры.
/// Ключи таблицы — прототипы сущностей или особые значения ("dud", "randomSeed", ...).
/// </summary>
[Prototype("imperialFishSource")]
public sealed partial class ImperialFishSourcePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>fish_table: относительные веса.</summary>
    [DataField]
    public Dictionary<string, int> Table = new();

    /// <summary>fish_counts: ограниченное количество.</summary>
    [DataField]
    public Dictionary<string, int> Counts = new();

    /// <summary>fish_count_regen; по умолчанию 30 минут для всего из Counts.</summary>
    [DataField]
    public Dictionary<string, TimeSpan> CountRegen = new();

    /// <summary>duds: что сказать, если ничего не поймали.</summary>
    [DataField]
    public List<LocId> Duds = new() { "imperial-fishing-dud-nothing", "imperial-fishing-dud-empty" };

    /// <summary>fishing_difficulty.</summary>
    [DataField]
    public float Difficulty = ImperialFishing.DefaultDifficulty;

    /// <summary>Фон мини-игры (состояние fishing_hud).</summary>
    [DataField]
    public string Background = "background_default";

    /// <summary>catalog_description.</summary>
    [DataField]
    public LocId? CatalogDescription;

    /// <summary>wait_time_range в секундах.</summary>
    [DataField]
    public Vector2? WaitTimeRange;

    /// <summary>Нужна леска для лавы (лава, плазма).</summary>
    [DataField]
    public bool RequiresLavaLine;

    /// <summary>Это пропасть (крючок спасателя работает только здесь).</summary>
    [DataField]
    public bool Chasm;

    /// <summary>FISH_SOURCE_FLAG_EXPLOSIVE_MALUS.</summary>
    [DataField]
    public bool ExplosiveMalus;

    /// <summary>FISH_SOURCE_FLAG_EXPLOSIVE_NONE.</summary>
    [DataField]
    public bool ExplosiveNone;

    /// <summary>FISH_SOURCE_FLAG_NO_BLUESPACE_ROD.</summary>
    [DataField]
    public bool NoBluespaceRod;

    /// <summary>Поведение источника, требующее кода (см. ImperialFishSourceSystem).</summary>
    [DataField]
    public string? Special;

    /// <summary>Иконка в радиальном меню портала (radial_state).</summary>
    [DataField]
    public string RadialState = "default";

    /// <summary>Наложение на портал (overlay_state).</summary>
    [DataField]
    public string OverlayState = "portal_aquarium";

    /// <summary>
    /// Прототипы мест, на которых рыба этого источника не умирает и через время уплывает
    /// (associated_safe_turfs).
    /// </summary>
    [DataField]
    public List<EntProtoId> SafeSpots = new();
}

/// <summary>
/// Какие существующие сущности сборки считаются местами ловли (вода, лава, пропасть, унитаз и т. п.),
/// без изменения их прототипов.
/// </summary>
[Prototype("imperialFishingSpots")]
public sealed partial class ImperialFishingSpotsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Прототип сущности (с наследниками) → источник рыбы.</summary>
    [DataField]
    public Dictionary<EntProtoId, ProtoId<ImperialFishSourcePrototype>> Entities = new();

    /// <summary>Компонент сущности → источник рыбы (например, Toilet).</summary>
    [DataField]
    public Dictionary<string, ProtoId<ImperialFishSourcePrototype>> Components = new();
}

/// <summary>Черта рыбы (/datum/fish_trait). Поведение — в ImperialFishTraitSystem по ID.</summary>
[Prototype("imperialFishTrait")]
public sealed partial class ImperialFishTraitPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public List<ProtoId<ImperialFishTraitPrototype>> Incompatible = new();

    [DataField]
    public int Inheritability = 50;

    /// <summary>spontaneous_manifest_types: прототип рыбы → шанс в процентах.</summary>
    [DataField]
    public Dictionary<EntProtoId, int> Spontaneous = new();

    /// <summary>fish_whitelist.</summary>
    [DataField]
    public List<EntProtoId>? Whitelist;

    [DataField]
    public int AddedDifficulty;

    /// <summary>reagents_to_add: реагенты в съедобной рыбе (умножаются на число укусов).</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> Reagents = new();
}

/// <summary>Значки улова в мини-игре для мастеров рыбалки (GLOB.specific_fish_icons).</summary>
[Prototype("imperialFishingIcons")]
public sealed partial class ImperialFishingIconsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Прототип (с наследниками) → значок.</summary>
    [DataField]
    public Dictionary<string, ImperialFishIcon> Entities = new();

    /// <summary>Особые награды ("randomSeed", "randomOrgan", ...) → значок.</summary>
    [DataField]
    public Dictionary<string, ImperialFishIcon> Specials = new();
}

/// <summary>
/// Эволюция рыбы (/datum/fish_evolution): при размножении или росте вместо своего вида получается новый.
/// </summary>
[Prototype("imperialFishEvolution")]
public sealed partial class ImperialFishEvolutionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>name: если не задано — название нового вида.</summary>
    [DataField]
    public LocId? Name;

    [DataField]
    public int Probability;

    [DataField(required: true)]
    public EntProtoId NewFish;

    [DataField]
    public float TemperatureMin;

    [DataField]
    public float TemperatureMax = float.PositiveInfinity;

    [DataField]
    public List<ProtoId<ImperialFishTraitPrototype>> NewTraits = new();

    [DataField]
    public List<ProtoId<ImperialFishTraitPrototype>> RemovedTraits = new();

    [DataField]
    public LocId? ConditionsNote;

    /// <summary>Особое условие (check_conditions): fedLube, mastodon, chainsawfish, armoredPike, moonfish, dwarfMoonfish.</summary>
    [DataField]
    public string? Condition;

    /// <summary>show_result_on_wiki: в каталоге результат скрыт («???»).</summary>
    [DataField]
    public bool HideResult;
}
