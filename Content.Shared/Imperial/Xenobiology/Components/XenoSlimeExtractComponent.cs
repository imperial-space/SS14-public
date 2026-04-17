using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>
/// Компонент экстракта ксено-слайма.
/// При инъекции крови, плазмы или воды в раствор экстракта — срабатывает
/// соответствующий эффект согласно таблице SS13 Xenobiology.
/// </summary>
[RegisterComponent]
public sealed partial class XenoSlimeExtractComponent : Component
{
    /// <summary>Цвет слайма, из которого получен этот экстракт. Используется в механике кроссбридинга.</summary>
    [DataField]
    public XenoSlimeColor ExtractColor = XenoSlimeColor.Grey;

    /// <summary>Название раствора в SolutionContainerManager для мониторинга триггеров.</summary>
    [DataField]
    public string SolutionName = "slime";

    /// <summary>ID реагента-триггера для эффекта крови (по умолчанию: Blood).</summary>
    [DataField]
    public string BloodReagentId = "Blood";

    /// <summary>ID реагента-триггера для эффекта плазмы (по умолчанию: Plasma).</summary>
    [DataField]
    public string PlasmaReagentId = "Plasma";

    /// <summary>ID реагента-триггера для водяного эффекта (по умолчанию: Water).</summary>
    [DataField]
    public string WaterReagentId = "Water";

    /// <summary>Минимальное количество реагента для срабатывания (в единицах).</summary>
    [DataField]
    public float TriggerMinAmount = 1f;

    /// <summary>Эффект при инъекции крови.</summary>
    [DataField]
    public XenoExtractEffect? BloodEffect = null;

    /// <summary>Эффект при инъекции плазмы.</summary>
    [DataField]
    public XenoExtractEffect? PlasmaEffect = null;

    /// <summary>Эффект при инъекции воды.</summary>
    [DataField]
    public XenoExtractEffect? WaterEffect = null;

    /// <summary>Был ли экстракт уже активирован.</summary>
    public bool Used = false;
}

/// <summary>
/// Описание одного эффекта экстракта слайма.
/// Используется в полях BloodEffect, PlasmaEffect, WaterEffect компонента.
/// </summary>
[DataDefinition]
public sealed partial class XenoExtractEffect
{
    /// <summary>Тип эффекта.</summary>
    [DataField(required: true)]
    public XenoExtractEffectType Type;

    // ── SpawnItems / SpawnMob ────────────────────────────────────────────────
    /// <summary>Список прототипов сущностей для спавна (для SpawnItems).</summary>
    [DataField]
    public List<EntProtoId> SpawnEntities = new();

    // ── ProduceReagent ───────────────────────────────────────────────────────
    /// <summary>ID реагента для производства (для ProduceReagent).</summary>
    [DataField]
    public string? ProduceReagent = null;

    /// <summary>Количество производимого реагента в единицах.</summary>
    [DataField]
    public float ProduceAmount = 25f;

    // ── EmpPulse ─────────────────────────────────────────────────────────────
    /// <summary>Радиус ЭМИ импульса в тайлах.</summary>
    [DataField]
    public float EmpRange = 4f;

    /// <summary>Потребление энергии ЭМИ импульса.</summary>
    [DataField]
    public float EmpEnergyConsumption = 200f;

    /// <summary>Длительность ЭМИ эффекта.</summary>
    [DataField]
    public TimeSpan EmpDuration = TimeSpan.FromSeconds(20);

    // ── StartFire ────────────────────────────────────────────────────────────
    /// <summary>Радиус поджигания (в тайлах).</summary>
    [DataField]
    public float FireRange = 2.5f;

    /// <summary>Интенсивность огня (0–1).</summary>
    [DataField]
    public float FireSeverity = 0.5f;

    // ── Explosion ────────────────────────────────────────────────────────────
    /// <summary>Тип взрыва.</summary>
    [DataField]
    public string ExplosionTypeId = "Default";

    /// <summary>Суммарная интенсивность взрыва.</summary>
    [DataField]
    public float ExplosionTotalIntensity = 80f;

    /// <summary>Уклон взрыва.</summary>
    [DataField]
    public float ExplosionSlope = 5f;

    /// <summary>Максимальная интенсивность взрыва на тайл.</summary>
    [DataField]
    public float ExplosionMaxTileIntensity = 12f;

    // ── MakeSlimesBerserk ────────────────────────────────────────────────────
    /// <summary>Радиус агрессии слаймов (в тайлах).</summary>
    [DataField]
    public float BerserkerRange = 6f;

    // ── SpawnMob ─────────────────────────────────────────────────────────────
    /// <summary>Прототип существа для спавна (для SpawnMob).</summary>
    [DataField]
    public EntProtoId? MobPrototype = null;

    /// <summary>True — существо пассивно (нейтральное).</summary>
    [DataField]
    public bool MobIsNeutral = false;

    /// <summary>True — существо дружелюбное (не атакует).</summary>
    [DataField]
    public bool MobIsFriendly = false;

    // ── SpawnRandomSlime ─────────────────────────────────────────────────────
    /// <summary>Пул случайных слаймов для SpawnRandomSlime (если пусто — берёт случайный цвет).</summary>
    [DataField]
    public List<EntProtoId> RandomSlimePool = new();

    // ── SpawnRandomFromPool ──────────────────────────────────────────────────
    /// <summary>Пул сущностей для случайного выбора (для SpawnRandomFromPool).</summary>
    [DataField]
    public List<EntProtoId> RandomPool = new();

    // ── SnapCool ─────────────────────────────────────────────────────────────
    /// <summary>Целевая температура заморозки (К).</summary>
    [DataField]
    public float SnapCoolTemperature = 200f;

    /// <summary>Радиус заморозки (тайлы).</summary>
    [DataField]
    public float SnapCoolRange = 2f;
    // ── SnapHeat ────────────────────────────────────────────────────────────────
    /// <summary>Целевая температура нагрева (К).</summary>
    [DataField]
    public float SnapHeatTemperature = 2000f;

    /// <summary>Радиус нагрева (тайлы).</summary>
    [DataField]
    public float SnapHeatRange = 3f;
    // ── Общее ────────────────────────────────────────────────────────────────
    /// <summary>Сообщение, показываемое при активации (popup).</summary>
    [DataField]
    public string? UseMessage = null;
}

/// <summary>Типы эффектов экстракта слайма.</summary>
public enum XenoExtractEffectType : byte
{
    /// <summary>Нет эффекта.</summary>
    None,

    /// <summary>Спавнит сущности из SpawnEntities на месте экстракта.</summary>
    SpawnItems,

    /// <summary>Помещает реагент ProduceReagent в раствор экстракта для изъятия шприцем.</summary>
    ProduceReagent,

    /// <summary>Создаёт ЭМИ импульс вокруг экстракта.</summary>
    EmpPulse,

    /// <summary>Поджигает тайлы вокруг экстракта.</summary>
    StartFire,

    /// <summary>Создаёт взрыв на месте экстракта.</summary>
    Explosion,

    /// <summary>Переводит ближайших слаймов в агрессивное состояние.</summary>
    MakeSlimesBerserk,

    /// <summary>Спавнит существо MobPrototype вблизи экстракта.</summary>
    SpawnMob,

    /// <summary>Спавнит случайного слайма.</summary>
    SpawnRandomSlime,

    /// <summary>Резко охлаждает тайлы вокруг экстракта.</summary>
    SnapCool,

    /// <summary>Спавнит одну случайную сущность из RandomPool.</summary>
    SpawnRandomFromPool,

    /// <summary>Резко нагревает тайлы вокруг экстракта.</summary>
    SnapHeat,
}
