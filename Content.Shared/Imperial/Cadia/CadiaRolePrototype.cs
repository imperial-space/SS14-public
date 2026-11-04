using Content.Shared.Dataset;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

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
