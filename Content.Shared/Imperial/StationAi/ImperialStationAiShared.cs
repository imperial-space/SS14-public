using Content.Shared.Actions;
using Robust.Shared.GameStates;
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

/// <summary>announcement(): объявление голосом VOX.</summary>
public sealed partial class ImperialAiAnnouncementActionEvent : InstantActionEvent;

/// <summary>pick_status_display(): картинка на статус-дисплеях станции.</summary>
public sealed partial class ImperialAiStatusDisplayActionEvent : InstantActionEvent;

/// <summary>Сервер → клиент: открыть окно объявления (последний текст и сколько ждать).</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiAnnouncementOpenEvent(string lastMessage, float cooldown) : EntityEventArgs
{
    public string LastMessage = lastMessage;
    public float Cooldown = cooldown;
}

/// <summary>Клиент → сервер: текст объявления.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiAnnouncementEvent(string message) : EntityEventArgs
{
    public string Message = message;
}

/// <summary>Сервер → клиенты: проиграть слова VOX по очереди.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiVoxPlayEvent(List<string> words) : EntityEventArgs
{
    public List<string> Words = words;
}

/// <summary>Клиент → сервер: выбрана картинка дисплеев (null — «Нет»).</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiStatusDisplayChoiceEvent(string? state) : EntityEventArgs
{
    public string? State = state;
}

/// <summary>Сервер → клиент: окно выбора картинки с текущей.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiStatusDisplayOpenEvent(string? current) : EntityEventArgs
{
    public string? Current = current;
}

/// <summary>Картинка ИИ поверх экрана (status_display/ai).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class ImperialAiDisplayComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? State;
}

public static class ImperialAiVox
{
    public const string Directory = "/Audio/Imperial/StationAi/Vox";

    /// <summary>VOX_DELAY: 60 секунд между объявлениями.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    /// <summary>Не больше 30 слов за раз.</summary>
    public const int MaxWords = 30;

    /// <summary>Слово VOX → имя файла: знаки препинания хранятся как _comma и _period.</summary>
    public static string FileOf(string word)
    {
        return word switch
        {
            "," => "_comma",
            "." => "_period",
            _ => word,
        };
    }

    public static string PathOf(string word)
    {
        return $"{Directory}/{FileOf(word)}.ogg";
    }

    /// <summary>GLOB.ai_status_display_emotes + ai_core_to_status_display_mapping: название → состояние.</summary>
    public static readonly (string Name, string State)[] DisplayOptions =
    {
        ("Awesome", "ai_awesome"),
        ("Blank", "ai_off"),
        ("Blue Glow", "ai_sal"),
        ("Confused", "ai_confused"),
        ("Dorfy", "ai_urist"),
        ("Facepalm", "ai_facepalm"),
        ("Friend Computer", "ai_friend"),
        ("Happy", "ai_happy"),
        ("Neutral", "ai_neutral"),
        ("Problems?", "ai_trollface"),
        ("Red Glow", "ai_hal"),
        ("Sad", "ai_sad"),
        ("Thinking", "ai_thinking"),
        ("Unsure", "ai_unsure"),
        ("Very Happy", "ai_veryhappy"),
        ("Dead", "ai_off"),
        ("Download", "ai_download"),
        ("Alien", "ai_status_alien"),
        ("Angel", "ai_status_angel"),
        ("Banned", "ai_status_banned"),
        ("Bliss", "ai_status_bliss"),
        ("Blue", "ai_status_blue"),
        ("Clown", "ai_status_clown"),
        ("Database", "ai_status_database"),
        ("Dorf", "ai_status_dorf"),
        ("Firewall", "ai_status_firewall"),
        ("Fuzzy", "ai_status_fuzzy"),
        ("Gentoo", "ai_status_gentoo"),
        ("Glitchman", "ai_status_glitchman"),
        ("Gondola", "ai_status_gondola"),
        ("Goon", "ai_status_goon"),
        ("Hades", "ai_status_hades"),
        ("HAL 9000", "ai_status_hal9000"),
        ("Heartline", "ai_status_heartline"),
        ("Helios", "ai_status_helios"),
        ("House", "ai_status_house"),
        ("Matrix", "ai_status_matrix"),
        ("Monochrome", "ai_status_monochrome"),
        ("Murica", "ai_status_murica"),
        ("Not Malf", "ai_status_not_malf"),
        ("President", "ai_status_president"),
        ("Rainbow", "ai_status_rainbow"),
        ("Red October", "ai_status_red_october"),
        ("Red", "ai_status_red"),
        ("Static", "ai_status_static"),
        ("Syndicat Meow", "ai_status_syndicat_meow"),
        ("Too Deep", "ai_status_too_deep"),
        ("Triumvirate", "ai_status_triumvirate"),
    };
}
