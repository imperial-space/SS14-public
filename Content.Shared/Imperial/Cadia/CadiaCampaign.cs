using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

// ═══════════════════════ Секторы ═══════════════════════

/// <summary>Что показать о секторе на панели войны.</summary>
[Serializable, NetSerializable]
public struct CadiaSectorInfo
{
    public string Letter;
    public CadiaFaction? Owner;
    public CadiaFaction? Capturer;
    public float Progress;
    public bool Contested;
    public System.Numerics.Vector2 Position;
    public int Map;
}

[Serializable, NetSerializable]
public enum CadiaLandmarkKind : byte
{
    Pylon,
    Citadel,
    Beacon,
}

/// <summary>Точка на поле для подсказки цели: клиент не видит далёкие сущности, поэтому координаты шлёт сервер.</summary>
[Serializable, NetSerializable]
public struct CadiaLandmark
{
    public CadiaLandmarkKind Kind;
    public int Number;
    public System.Numerics.Vector2 Position;
    public int Map;
}

/// <summary>Только что появившийся боец неуязвим несколько секунд или до первого выстрела.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaSpawnProtectionComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Until;
}

/// <summary>Строка ленты событий у всех игроков.</summary>
[Serializable, NetSerializable]
public sealed class CadiaFeedEvent(string text, Color color) : EntityEventArgs
{
    public readonly string Text = text;
    public readonly Color Color = color;
}

/// <summary>
/// Сектор фронта: знамя, которое берут, оставшись рядом без врагов. Хозяину даёт снабжение (варп у Хаоса) и,
/// начиная со штурма, точку высадки, пока за сектор не идёт бой.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CadiaSectorComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Letter = "?";

    [DataField]
    public float Radius = 10f;

    [DataField]
    public TimeSpan CaptureTime = TimeSpan.FromSeconds(90);

    [DataField, AutoNetworkedField]
    public CadiaFaction? Owner;

    [ViewVariables, AutoNetworkedField]
    public CadiaFaction? Capturer;

    [ViewVariables, AutoNetworkedField]
    public float Progress;

    [ViewVariables, AutoNetworkedField]
    public bool Contested;
}

// ═══════════════════════ Падение Кадии ═══════════════════════

/// <summary>
/// Маяк эвакуации: после падения Кадии сюда садятся транспорты и забирают тех, кто рядом.
/// Неуязвим до начала эвакуации; уничтоженный маяк означает конец спасения.
/// </summary>
[RegisterComponent]
public sealed partial class CadiaEvacBeaconComponent : Component
{
    [DataField]
    public float Radius = 7f;

    /// <summary>Сколько бойцов берёт один транспорт.</summary>
    [DataField]
    public int Capacity = 10;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(60);

    /// <summary>За сколько до посадки показывается транспорт.</summary>
    [DataField]
    public TimeSpan Warning = TimeSpan.FromSeconds(15);

    [ViewVariables]
    public TimeSpan? NextLoad;

    [ViewVariables]
    public bool ShipComing;
}

/// <summary>Транспорт спускается на площадку и улетает: сервер считает время, рисует клиент.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaEvacShipComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public TimeSpan LandAt;

    [DataField, AutoNetworkedField]
    public TimeSpan LeaveAt;

    [DataField, AutoNetworkedField]
    public TimeSpan GoneAt;
}

// ═══════════════════════ Варп-штормы ═══════════════════════

[Serializable, NetSerializable]
public enum CadiaWarpStorm : byte
{
    None,
    BloodRain,
    Whispers,
    DaemonSurge,
}

[Serializable, NetSerializable]
public enum CadiaWeather : byte
{
    Clear,
    Ash,
    Fog,
    Smog,
}

// ═══════════════════════ Концовки ═══════════════════════

/// <summary>Чем кончилась война: по этому выбирается финальная сцена.</summary>
[Serializable, NetSerializable]
public enum CadiaEnding : byte
{
    Held,
    Banished,
    Exterminatus,
    Retreat,
    Bitter,
    Annihilation,
    Triumph,
}

/// <summary>Показать финальную сцену.</summary>
[Serializable, NetSerializable]
public sealed class CadiaEndingEvent(CadiaEnding ending, int saved) : EntityEventArgs
{
    public readonly CadiaEnding Ending = ending;
    public readonly int Saved = saved;
}

// ═══════════════════════ Медали и памятка ═══════════════════════

/// <summary>Сколько медалей ещё может вручить этот офицер.</summary>
[RegisterComponent]
public sealed partial class CadiaMedalGiverComponent : Component
{
    [DataField]
    public int Left = 3;

    [ViewVariables]
    public EntityUid? Action;
}

public sealed partial class CadiaAwardMedalEvent : EntityTargetActionEvent;

public sealed partial class CadiaBriefingActionEvent : InstantActionEvent;

/// <summary>Открыть памятку роли у бойца.</summary>
[Serializable, NetSerializable]
public sealed class CadiaBriefingEvent(string role, string name) : EntityEventArgs
{
    public readonly string Role = role;
    public readonly string Name = name;
}

/// <summary>Показать вступительную сцену.</summary>
[Serializable, NetSerializable]
public sealed class CadiaIntroEvent : EntityEventArgs;

// ═══════════════════════ Панель ведущего ═══════════════════════

[Serializable, NetSerializable]
public enum CadiaPanelAction : byte
{
    Start,
    NextPhase,
    WaveImperium,
    WaveChaos,
    WinImperium,
    WinChaos,
    BeginFall,
    AnnounceAll,
    AnnounceImperium,
    AnnounceChaos,
    StormBloodRain,
    StormWhispers,
    StormDaemonSurge,
    AddWarp,
    CycleWeather,
    ToggleCloseImperium,
    ToggleCloseChaos,
    RemovePlayer,
    BanPlayer,
    ShowIntro,
    VeilToggle,
    VeilAddMinute,
    VeilCutMinute,
    DropRecharge,
    DropPod,
}

[Serializable, NetSerializable]
public sealed class CadiaPanelOpenEvent : EntityEventArgs;

/// <summary>Окно открыто или закрыто: подписаться на состояние или отписаться. Сервер проверяет права.</summary>
[Serializable, NetSerializable]
public sealed class CadiaPanelSubscribeEvent(bool subscribe) : EntityEventArgs
{
    public readonly bool Subscribe = subscribe;
}

[Serializable, NetSerializable]
public sealed class CadiaPanelActionEvent(CadiaPanelAction action, string text, Guid? target) : EntityEventArgs
{
    public readonly CadiaPanelAction Action = action;
    public readonly string Text = text;
    public readonly Guid? Target = target;
}

[Serializable, NetSerializable]
public struct CadiaPanelPlayer
{
    public Guid User;
    public string Name;
    public string Character;
    public CadiaFaction Faction;
    public string Role;
    public bool Alive;
    public bool Banned;
    public int TeamKills;
    public int TeamDamage;
}

[Serializable, NetSerializable]
public sealed class CadiaPanelStateEvent : EntityEventArgs
{
    public bool Active;
    public CadiaWarPhase Phase;
    public int SecondsLeft;
    public int PylonsAlive;
    public int PylonsTotal;
    public int Warp;
    public int ImperiumAlive;
    public int ChaosAlive;
    public int ImperiumSeats;
    public int ChaosSeats;
    public bool ImperiumForced;
    public bool ChaosForced;
    public int Saved;
    public bool VeilUp;
    public int RefugeesSaved;
    public int RefugeesTotal;
    public List<CadiaPanelPlayer> Players = new();
}

/// <summary>Варп-врата: переносят слугу Хаоса между кораблями флота еретиков.</summary>
[RegisterComponent]
public sealed partial class CadiaWarpGateComponent : Component
{
    /// <summary>Как подписать врата в списке у других врат; без названия берётся имя корабля.</summary>
    [DataField]
    public LocId? Label;

    [ViewVariables]
    public TimeSpan NextUse;
}
