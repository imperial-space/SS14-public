using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

// Кнопки HUD ИИ из TG (_onclick/hud/ai.dm), которых нет в ванильном SS14.

/// <summary>show_camera_list(): список камер станции.</summary>
public sealed partial class ImperialAiCameraListActionEvent : InstantActionEvent;

/// <summary>ai_camera_track(): слежение за существом через камеры.</summary>
public sealed partial class ImperialAiTrackActionEvent : InstantActionEvent;

/// <summary>open_nearest_door(): ближайший шлюз у отслеживаемой цели.</summary>
public sealed partial class ImperialAiOpenDoorActionEvent : InstantActionEvent;

/// <summary>toggle_camera_light(): подсветка камер рядом с «глазом».</summary>
public sealed partial class ImperialAiCameraLightActionEvent : InstantActionEvent;

/// <summary>Тревоги станции (station alert console ИИ).</summary>
public sealed partial class ImperialAiAlertsActionEvent : InstantActionEvent;

/// <summary>checklaws(): огласить законы.</summary>
public sealed partial class ImperialAiStateLawsActionEvent : InstantActionEvent;

/// <summary>toggle_sensors(): медицинский и охранный HUD.</summary>
public sealed partial class ImperialAiSensorsActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed class ImperialAiListEntry(NetEntity entity, string name, string detail)
{
    public NetEntity Entity = entity;
    public string Name = name;
    public string Detail = detail;
}

[Serializable, NetSerializable]
public enum ImperialAiListKind : byte
{
    Cameras,
    Track,
}

/// <summary>Сервер → клиент: список для tgui_input_list (камеры или цели слежения).</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiListEvent(ImperialAiListKind kind, List<ImperialAiListEntry> entries) : EntityEventArgs
{
    public ImperialAiListKind Kind = kind;
    public List<ImperialAiListEntry> Entries = entries;
}

/// <summary>Клиент → сервер: выбран пункт списка.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiListChoiceEvent(ImperialAiListKind kind, NetEntity entity) : EntityEventArgs
{
    public ImperialAiListKind Kind = kind;
    public NetEntity Entity = entity;
}

[Serializable, NetSerializable]
public enum ImperialAiAlarmKind : byte
{
    Fire,
    Atmosphere,
    Power,
    Camera,
}

[Serializable, NetSerializable]
public sealed class ImperialAiAlarmEntry(ImperialAiAlarmKind kind, NetEntity source, string location)
{
    public ImperialAiAlarmKind Kind = kind;
    public NetEntity Source = source;
    public string Location = location;
}

/// <summary>Сервер → клиент: окно тревог станции.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiAlertsEvent(List<ImperialAiAlarmEntry> alarms) : EntityEventArgs
{
    public List<ImperialAiAlarmEntry> Alarms = alarms;
}

/// <summary>Клиент → сервер: «перейти» к источнику тревоги.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiJumpEvent(NetEntity target) : EntityEventArgs
{
    public NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class ImperialAiChannelEntry(string id, string name)
{
    public string Id = id;
    public string Name = name;
}

/// <summary>Сервер → клиент: законы с номерами и доступные каналы.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiLawsEvent(List<string> laws, List<ImperialAiChannelEntry> channels) : EntityEventArgs
{
    public List<string> Laws = laws;
    public List<ImperialAiChannelEntry> Channels = channels;
}

/// <summary>Клиент → сервер: огласить отмеченные законы на канале (пустой канал — вслух).</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiStateLawsEvent(List<int> laws, string? channel) : EntityEventArgs
{
    public List<int> Laws = laws;
    public string? Channel = channel;
}
