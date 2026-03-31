using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>
/// Компонент ксено-слайма.
///
/// Состояния настроения:
///   Neutral    — пассивен, атакует только обезьян.
///   Aggressive — голодный, атакует всех подряд.
///
/// Система дружбы:
///   Когда слайм ест, он запоминает игроков рядом.
///   После 2 кормлений рядом — игрок становится другом, слайм его не атакует.
/// </summary>
[RegisterComponent]
public sealed partial class XenoSlimeComponent : Component
{
    /// <summary>Цвет этого слайма — определяет прототипы потомков.</summary>
    [DataField]
    public XenoSlimeColor Color = XenoSlimeColor.Grey;

    /// <summary>True — взрослый (большой) слайм. False — детёныш (маленький).</summary>
    [DataField]
    public bool IsAdult = false;

    // -- Голод ----------------------------------------------------------------

    /// <summary>
    /// Уровень сытости (0..100 %).
    /// 0 % — слайм голоден и переходит в агрессию.
    /// 100 % — слайм сыт и делится/вырастает.
    /// Спаун-значение: 50 %. Обезьяна даёт +70 %.
    /// Падает со скоростью 100 % за 4 минуты.
    /// </summary>
    public float HungerPercent = 50f;

    /// <summary>Текущее настроение слайма.</summary>
    public XenoSlimeMood Mood = XenoSlimeMood.Neutral;
    // -- Переваривание (таймер роста) -------------------------------------

    /// <summary>Флаг: слайм сейчас переваривает пищу (2 мин до роста/деления).</summary>
    public bool IsDigesting = false;

    /// <summary>Таймер переваривания (секунды). Сбрасывается при начале переваривания.</summary>
    public float DigestTimer = 0f;

    /// <summary>Время переваривания (120 с = 2 минуты).</summary>
    public const float DigestDuration = 120f;
    // -- Друзья ---------------------------------------------------------------

    /// <summary>
    /// Счётчик кормлений: сколько раз каждая сущность была рядом в момент поедания.
    /// Не сериализуется — рантайм-только.
    /// </summary>
    public readonly Dictionary<EntityUid, int> FeedCounts = new();

    /// <summary>Сущности с FeedCounts >= 2 — друзья, слайм их не атакует.</summary>
    public readonly HashSet<EntityUid> Friends = new();

    /// <summary>Радиус (тайлы) поиска «кормильца» рядом с едой в момент поедания.</summary>
    [DataField]
    public float FeederDetectRange = 3f;

    // -- Поглощение (желудок) ------------------------------------------------

    /// <summary>ID контейнера-желудка, инициализируется XenoSlimeSystem при старте.</summary>
    public const string StomachContainerId = "xeno_slime_stomach";

    /// <summary>
    /// Entity, которую слайм сейчас проглатывает (DoAfter запущен). Null — не ест.
    /// Используется для защиты от повторного запуска DoAfter.
    /// </summary>
    public EntityUid? SwallowTarget = null;

    /// <summary>Сколько секунд занимает процесс проглатывания (DoAfter длительность).</summary>
    [DataField]
    public float SwallowDuration = 3f;

    // -- Тир цвета (мутационная система) -------------------------------------

    /// <summary>
    /// Тир цвета слайма (SS13-стиль):
    ///   0 = Grey (стартовый)
    ///   1 = Orange, Purple, Blue, Metal
    ///   2 = Yellow, DarkPurple, DarkBlue, Silver
    ///   3 = Bluespace, Sepia, Cerulean, Pyrite  (SS13 тир 2.5)
    ///   4 = Green, Red, Pink, Gold              (SS13 тир 3)
    ///   5 = Oil, Black, LightPink, Adamantine   (SS13 тир 4)
    ///   6 = Rainbow                             (SS13 тир 5)
    /// Влияет на шанс мутации:
    ///   Чем ниже тир, тем выше шанс что потомок будет другого цвета.
    /// </summary>
    [DataField]
    public byte Tier = 0;

    // -- Баффы (применяются инъекцией реагента) -----------------------------

    /// <summary>
    /// Уровень стабилизации (0–3). Каждый уровень снижает шанс мутации потомков
    /// на 15%. Наследуется через поколения.
    /// Получается инъекцией реагента SlimeStabilizer (из синего экстракта).
    /// </summary>
    [DataField]
    public byte StabilizationLevel = 0;

    /// <summary>
    /// Количество доз стероида (0–3). Каждая доза даёт +1 дополнительный экстракт
    /// при обработке в дробилке. Максимум 3 дозы → +3 экстракта сверх базового.
    /// Получается инъекцией реагента SlimeSteroid (из фиолетового экстракта).
    /// </summary>
    [DataField]
    public byte SteroidCount = 0;

    /// <summary>
    /// Временный бонус мутации (0–3). Снижает эффективный тир при вычислении шанса мутации,
    /// тем самым повышая шанс получения нового цвета потомков.
    /// Применяется реагентом SlimeMutationPotion (из красного экстракта). Сбрасывается после размножения.
    /// </summary>
    [DataField]
    public byte MutationBoost = 0;

    // -- Возраст (стадии Old / Ancient) ---------------------------------------

    /// <summary>
    /// Секунд прожито с момента спавна. Не сбрасывается при поглощении еды.
    /// По достижении OldAgeThreshold (600 с = 10 мин) взрослый слайм превращается в Old.
    /// По достижении AncientAgeThreshold (1200 с = 20 мин) слайм превращается в Ancient.
    /// </summary>
    public float AgeSeconds = 0f;

    /// <summary>
    /// Возрастная стадия. Устанавливается автоматически системой.
    /// Начальное значение для Old/Ancient прототипов выставляется YAML-датафилдом.
    /// </summary>
    [DataField]
    public XenoSlimeAge AgeStage = XenoSlimeAge.Young;

    /// <summary>Порог (сек) перехода в стадию Old.</summary>
    public const float OldAgeThreshold = 600f;     // 10 минут

    /// <summary>Порог (сек) перехода в стадию Ancient.</summary>
    public const float AncientAgeThreshold = 1200f; // 20 минут
}

/// <summary>Настроение ксено-слайма.</summary>
public enum XenoSlimeMood : byte
{
    /// <summary>Пассивен — атакует только обезьян (фракция XenoSlimeFaction).</summary>
    Neutral,
    /// <summary>Голодный — атакует всех (фракция SimpleHostile).</summary>
    Aggressive,
}

/// <summary>
/// Цвета ксено-слаймов (SS13 Xenobiology).
/// Порядок должен совпадать с массивами SmallProtos/LargeProtos в XenoSlimeSystem.
///
/// Схема тиров:
///   Тир 0: Grey
///   Тир 1: Orange, Purple, Blue, Metal
///   Тир 2: Yellow, DarkPurple, DarkBlue, Silver
///   Тир 3 (SS13 2.5): Bluespace, Sepia, Cerulean, Pyrite
///   Тир 4 (SS13 3):   Green, Red, Pink, Gold
///   Тир 5 (SS13 4):   Oil, Black, LightPink, Adamantine
///   Тир 6 (SS13 5):   Rainbow
/// </summary>
public enum XenoSlimeColor : byte
{
    Grey        =  0,  // тир 0
    Orange      =  1,  // тир 1
    Purple      =  2,  // тир 1
    Blue        =  3,  // тир 1
    Metal       =  4,  // тир 1
    Yellow      =  5,  // тир 2
    DarkPurple  =  6,  // тир 2
    DarkBlue    =  7,  // тир 2
    Silver      =  8,  // тир 2
    Bluespace   =  9,  // тир 3 (SS13 2.5)
    Sepia       = 10,  // тир 3 (SS13 2.5)
    Cerulean    = 11,  // тир 3 (SS13 2.5)
    Pyrite      = 12,  // тир 3 (SS13 2.5)
    Green       = 13,  // тир 4 (SS13 3)
    Red         = 14,  // тир 4 (SS13 3)
    Pink        = 15,  // тир 4 (SS13 3)
    Gold        = 16,  // тир 4 (SS13 3)
    Oil         = 17,  // тир 5 (SS13 4)
    Black       = 18,  // тир 5 (SS13 4)
    LightPink   = 19,  // тир 5 (SS13 4)
    Adamantine  = 20,  // тир 5 (SS13 4)
    Rainbow     = 21,  // тир 6 (SS13 5)
}
/// <summary>Возрастная стадия ксено-слайма.</summary>
public enum XenoSlimeAge : byte
{
    /// <summary>Любой слайм (Small или Large), младше 10 минут.</summary>
    Young = 0,
    /// <summary>Взрослый (Large/Adult) слайм — промежуточное состояние после поедания первой жертвы.</summary>
    Adult = 1,
    /// <summary>Старый (≥ 10 мин). Больше, крепче, при делении даёт 1 Large + 2-3 Small.</summary>
    Old = 2,
    /// <summary>Древний (≥ 20 мин). Самый мощный, при делении порождает 1 Old + 1 Large + 2 Small.</summary>
    Ancient = 3,
}