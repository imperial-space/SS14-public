using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.BossLoot;

/// <summary>
/// Описывает один элемент таблицы лута босса.
/// </summary>
[DataDefinition]
public sealed partial class BossLootEntry
{
    /// <summary>Прототип предмета для спавна.</summary>
    [DataField(required: true)]
    public EntProtoId Prototype = default!;

    /// <summary>Минимальное количество (включительно).</summary>
    [DataField]
    public int MinCount = 1;

    /// <summary>Максимальное количество (включительно).</summary>
    [DataField]
    public int MaxCount = 1;

    /// <summary>Шанс выпадения от 0.0 до 1.0. 1.0 = 100%.</summary>
    [DataField]
    public float Chance = 1.0f;
}

/// <summary>
/// Добавьте на босса, чтобы при его смерти автоматически выпадал лут из таблицы <see cref="Loot"/>.
/// Поддерживает шанс и диапазон количества для каждого предмета.
/// </summary>
[RegisterComponent]
public sealed partial class BossLootComponent : Component
{
    /// <summary>Таблица лута. Каждый элемент может иметь свой шанс и количество.</summary>
    [DataField]
    public List<BossLootEntry> Loot = new();

    /// <summary>Флаг защиты от двойного дропа.</summary>
    [ViewVariables]
    public bool LootDropped;
}
