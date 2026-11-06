using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Fishing;

/// <summary>
/// Источник рыбы (/datum/fish_source): таблица улова, сложность, фон мини-игры.
/// Ключи таблицы — прототипы сущностей или особые значения ("dud", "randomSeed", ...).
/// </summary>
[Prototype]
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
