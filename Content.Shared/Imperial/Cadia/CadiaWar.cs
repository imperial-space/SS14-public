using Content.Shared.Dataset;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Сторона войны за Кадию.</summary>
[Serializable, NetSerializable]
public enum CadiaFaction : byte
{
    Imperium,
    Chaos,
}

/// <summary>
/// Фазы войны: подготовка обороны → орбитальный обстрел → штурм → развязка (высадка Чёрного легиона).
/// </summary>
[Serializable, NetSerializable]
public enum CadiaWarPhase : byte
{
    Preparation,
    Bombardment,
    Assault,
    Finale,
    /// <summary>Кадия пала: защитники уходят транспортами, Хаос пытается никого не выпустить.</summary>
    Evacuation,
    Ended,
}

/// <summary>
/// Роль войны: сторона, снаряжение, моб и сколько таких бойцов может быть в живых одновременно.
/// Свободные места пополняются волнами подкреплений своей стороны.
/// </summary>
[Prototype]
public sealed partial class CadiaRolePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    /// <summary>Что сказать бойцу при появлении: задача и как играть роль.</summary>
    [DataField]
    public LocId? Briefing;

    [DataField(required: true)]
    public CadiaFaction Faction;

    /// <summary>Кем появляется боец (человек, космодесантник и т.д.).</summary>
    [DataField(required: true)]
    public EntProtoId Mob;

    [DataField]
    public ProtoId<StartingGearPrototype>? Gear;

    /// <summary>Сколько бойцов этой роли может быть в живых одновременно (на 100 игроков).</summary>
    [DataField]
    public int Slots = 1;

    /// <summary>С какой фазы роль открывается (космодесант прибывает к штурму, Чёрный легион — к развязке).</summary>
    [DataField]
    public CadiaWarPhase AvailableFrom = CadiaWarPhase.Preparation;

    /// <summary>Значок роли над головой (видят только свои).</summary>
    [DataField(required: true)]
    public ProtoId<FactionIconPrototype> Icon;

    /// <summary>Звание перед именем: «Рядовой», «Комиссар», «Брат».</summary>
    [DataField]
    public LocId? Rank;

    /// <summary>Брать ли случайное имя (у героев вроде лорда-кастеляна — своё).</summary>
    [DataField]
    public bool RandomName = true;

    /// <summary>Откуда брать имя: «Ян», «Варрон», «Скарр».</summary>
    [DataField]
    public ProtoId<DatasetPrototype>? FirstNames;

    /// <summary>Откуда брать фамилию (у космодесантников её нет).</summary>
    [DataField]
    public ProtoId<DatasetPrototype>? LastNames;

    /// <summary>Командир: может формировать отряды и писать на голокарте.</summary>
    [DataField]
    public bool Commander;

    /// <summary>Что роль получает при появлении: силы псайкера, органы, метки, действия.</summary>
    [DataField]
    public ComponentRegistry? Components;

    /// <summary>Группа точек высадки (флот — на корабле); без группы — общие точки стороны.</summary>
    [DataField]
    public string? SpawnGroup;

    /// <summary>Роль только для игроков из вайтлиста (cadia whitelist add).</summary>
    [DataField]
    public bool Whitelisted;

    /// <summary>Порядок в списке ролей (меньше — выше).</summary>
    [DataField]
    public int Order;

    /// <summary>Разделы памятки: как устроена именно эта роль.</summary>
    [DataField]
    public List<LocId> Tips = new();

    /// <summary>Сколько медалей может вручить. Без указания у командиров 3, у остальных нет.</summary>
    [DataField]
    public int? Medals;
}

/// <summary>Боец войны за Кадию: сторона и роль. По ним свои видят значки и отряды.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaFactionMemberComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaFaction Faction;

    [DataField, AutoNetworkedField]
    public ProtoId<CadiaRolePrototype>? Role;
}

/// <summary>
/// Общее состояние войны для интерфейса: фаза и её конец, пилоны, захват цитадели, сила варпа, следующие волны.
/// Висит на одной глобальной сущности, которую видят все клиенты.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaWarStateComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaWarPhase Phase;

    [DataField, AutoNetworkedField]
    public TimeSpan PhaseStart;

    [DataField, AutoNetworkedField]
    public TimeSpan PhaseEnd;

    [DataField, AutoNetworkedField]
    public int PylonsTotal;

    [DataField, AutoNetworkedField]
    public int PylonsAlive;

    /// <summary>0..1 — насколько Хаос захватил цитадель.</summary>
    [DataField, AutoNetworkedField]
    public float CitadelProgress;

    /// <summary>Цитадель сейчас оспаривается (внутри есть обе стороны).</summary>
    [DataField, AutoNetworkedField]
    public bool CitadelContested;

    /// <summary>Сила варпа: растёт с каждым павшим пилоном и убитым защитником, питает призывы Хаоса.</summary>
    [DataField, AutoNetworkedField]
    public int WarpPower;

    [DataField, AutoNetworkedField]
    public TimeSpan NextImperiumWave;

    [DataField, AutoNetworkedField]
    public TimeSpan NextChaosWave;

    [DataField, AutoNetworkedField]
    public CadiaFaction? Winner;

    /// <summary>Сколько мест открыто сейчас: для экрана ожидания и панели ведущего.</summary>
    [DataField, AutoNetworkedField]
    public int ImperiumSeats;

    [DataField, AutoNetworkedField]
    public int ChaosSeats;

    /// <summary>Набор стороны закрыт: перекос или решение ведущего.</summary>
    [DataField, AutoNetworkedField]
    public bool ImperiumClosed;

    [DataField, AutoNetworkedField]
    public bool ChaosClosed;

    /// <summary>Сколько защитников вывезено с падающей Кадии.</summary>
    [DataField, AutoNetworkedField]
    public int EvacSaved;

    /// <summary>Ведущий держит или снял завесу пилонов вручную; null — завеса держится, пока идёт подготовка.</summary>
    [DataField, AutoNetworkedField]
    public bool? VeilOverride;

    /// <summary>Сколько горожан Касра укрыто в катакомбах и сколько их было.</summary>
    [DataField, AutoNetworkedField]
    public int RefugeesSaved;

    [DataField, AutoNetworkedField]
    public int RefugeesTotal;

    [DataField, AutoNetworkedField]
    public TimeSpan NextTransport;

    [DataField, AutoNetworkedField]
    public CadiaWarpStorm Storm;

    [DataField, AutoNetworkedField]
    public TimeSpan StormEnd;

    [DataField, AutoNetworkedField]
    public List<CadiaSectorInfo> Sectors = new();

    [DataField, AutoNetworkedField]
    public List<CadiaLandmark> Landmarks = new();

    [DataField, AutoNetworkedField]
    public CadiaWeather Weather;

    [DataField, AutoNetworkedField]
    public TimeSpan WeatherStart;

    [DataField, AutoNetworkedField]
    public TimeSpan WeatherEnd;
}

/// <summary>
/// Кадийский пилон — древний обелиск, сдерживающий Око Ужаса. Пока пилоны стоят, варп слаб:
/// демоны ослаблены, призывы дороже. Падение всех пилонов — победа Хаоса.
/// До начала штурма пилоны неуязвимы (их защищает поле).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaPylonComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Shielded = true;

    /// <summary>Номер пилона для объявлений («Пилон III пал»).</summary>
    [DataField, AutoNetworkedField]
    public int Number;
}

[Serializable, NetSerializable]
public enum CadiaPylonVisuals : byte
{
    Shielded,
}

/// <summary>
/// Цитадель Касра: знамя, которое Хаос должен удержать в одиночку <see cref="CaptureTime"/>.
/// Защитники рядом останавливают захват и откатывают его.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaCitadelComponent : Component
{
    [DataField]
    public float Radius = 6f;

    [DataField]
    public TimeSpan CaptureTime = TimeSpan.FromMinutes(3);

    [DataField, AutoNetworkedField]
    public float Progress;

    [DataField, AutoNetworkedField]
    public bool Captured;
}

[Serializable, NetSerializable]
public enum CadiaCitadelVisuals : byte
{
    Captured,
}

/// <summary>Точка высадки стороны: здесь появляются её бойцы.</summary>
[RegisterComponent]
public sealed partial class CadiaSpawnPointComponent : Component
{
    [DataField]
    public CadiaFaction Faction;

    /// <summary>Только для ролей с той же <see cref="CadiaRolePrototype.SpawnGroup"/>.</summary>
    [DataField]
    public string? Group;

    /// <summary>Точка в секторе закрыта, пока за него идёт бой.</summary>
    [DataField]
    public bool Enabled = true;
}

/// <summary>
/// Края поля Кадии на карте этажа: клетки сетки планировки в мировых координатах. Ставится при постройке поля; по ним
/// сервер не выпускает за скалы, а клиент рисует буран у кромки.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaFieldBoundsComponent : Component
{
    [DataField, AutoNetworkedField]
    public Box2 Bounds;
}
