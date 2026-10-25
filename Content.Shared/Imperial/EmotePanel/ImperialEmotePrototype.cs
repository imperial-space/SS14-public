using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EmotePanel;

/// <summary>Кто может выполнить эмоцию (mob_type_allowed_typecache TG).</summary>
public enum ImperialEmoteMob : byte
{
    /// <summary>/mob/living: любое живое существо, кроме ИИ.</summary>
    Living,

    /// <summary>/mob/living/carbon: гуманоиды, обезьяны, ксеноморфы.</summary>
    Carbon,

    /// <summary>/mob/living/carbon/human.</summary>
    Human,

    Monkey,
    Alien,

    /// <summary>TRAIT_SILICON_EMOTES_ALLOWED: киборги и ИИ.</summary>
    Silicon,

    /// <summary>/mob/living/silicon/ai.</summary>
    Ai,
}

/// <summary>stat_allowed TG: в каком худшем состоянии ещё можно выполнить эмоцию.</summary>
public enum ImperialEmoteStat : byte
{
    Conscious,
    SoftCrit,
    Unconscious,
    HardCrit,
}

/// <summary>Особые проверки can_run_emote.</summary>
public enum ImperialEmoteRequirement : byte
{
    /// <summary>can_speak(allow_mimes = TRUE).</summary>
    CanSpeak,

    /// <summary>usable_hands >= 2.</summary>
    TwoHands,

    /// <summary>Обычные очки или солнцезащитные на глазах.</summary>
    Glasses,

    /// <summary>Хвост, которым можно вилять.</summary>
    Tail,

    /// <summary>Крылья.</summary>
    Wings,
}

/// <summary>Что эмоция делает помимо текста и звука (run_emote TG).</summary>
public enum ImperialEmoteEffect : byte
{
    None,
    Flip,
    Backflip,
    Spin,
    Jump,
    Taunt,
    Dance,
    Shiver,
    Sway,
    Tremble,
    Twitch,
    TwitchSlight,
    Tongue,
    Blush,
    Cry,
    Sigh,
    Glasses,
    Collapse,
    Faint,
    Surrender,
    Yawn,
    Wag,
    Point,
    AiDisplay,
}

/// <summary>/datum/emote TG: тексты лежат в локализации по ключам imperial-emote-{вид}-{id}.</summary>
[Prototype]
public sealed partial class ImperialEmotePrototype : IPrototype
{
    /// <summary>key эмоции TG.</summary>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ImperialEmoteMob> Mobs = new() { ImperialEmoteMob.Living };

    /// <summary>species_type_whitelist_typecache BandaStation и проверки ismoth и т.п.</summary>
    [DataField]
    public List<ProtoId<SpeciesPrototype>> Species = new();

    [DataField]
    public List<ImperialEmoteRequirement> Requires = new();

    /// <summary>EMOTE_AUDIBLE.</summary>
    [DataField]
    public bool Audible;

    /// <summary>EMOTE_VISIBLE.</summary>
    [DataField]
    public bool Visible = true;

    /// <summary>EMOTE_IMPORTANT.</summary>
    [DataField]
    public bool Important;

    /// <summary>hands_use_check.</summary>
    [DataField]
    public bool Hands;

    [DataField]
    public ImperialEmoteStat Stat = ImperialEmoteStat.Conscious;

    /// <summary>cooldown в секундах (3 по BandaStation).</summary>
    [DataField]
    public float Cooldown = 3f;

    [DataField]
    public ImperialEmoteEffect Effect = ImperialEmoteEffect.None;

    /// <summary>Картинка на экранах станции для эмоций ИИ.</summary>
    [DataField]
    public string? AiDisplay;

    /// <summary>Звук берётся из набора звуков вида (как у ванильной эмоции с этим id), если он там есть.</summary>
    [DataField]
    public ProtoId<EmotePrototype>? VanillaSound;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public SoundSpecifier? SoundMale;

    [DataField]
    public SoundSpecifier? SoundFemale;

    [DataField]
    public Dictionary<ProtoId<SpeciesPrototype>, SoundSpecifier> SpeciesSounds = new();

    [DataField]
    public bool Vary;

    /// <summary>manual_specific_emote_audio_cooldown в секундах.</summary>
    [DataField]
    public float SoundCooldown = 3f;

    public string NameLoc => $"imperial-emote-name-{ID}";

    /// <summary>Эмоция что-то делает: анимирует, сбивает с ног, меняет экраны.</summary>
    public bool IsAction => Effect != ImperialEmoteEffect.None;
}

/// <summary>Строка панели (ui_static_data TG): эмоция доступна этому существу.</summary>
[Serializable, NetSerializable]
public sealed class ImperialEmoteEntry
{
    public string Id = string.Empty;
    public bool Hands;
    public bool Visible;
    public bool Audible;
    public bool Sound;
    public bool Param;
    public bool Action;
}

/// <summary>Клиент → сервер: какие эмоции доступны.</summary>
[Serializable, NetSerializable]
public sealed class ImperialEmoteListRequest : EntityEventArgs;

/// <summary>Сервер → клиент: доступные эмоции.</summary>
[Serializable, NetSerializable]
public sealed class ImperialEmoteListMessage(List<ImperialEmoteEntry> emotes) : EntityEventArgs
{
    public readonly List<ImperialEmoteEntry> Emotes = emotes;
}

/// <summary>Клиент → сервер: play_emote с параметром (имя цели и т.п.).</summary>
[Serializable, NetSerializable]
public sealed class ImperialEmotePlayMessage(string id, string? param) : EntityEventArgs
{
    public readonly string Id = id;
    public readonly string? Param = param;
}

/// <summary>Клиент → сервер: preview_sound.</summary>
[Serializable, NetSerializable]
public sealed class ImperialEmotePreviewMessage(string id) : EntityEventArgs
{
    public readonly string Id = id;
}

/// <summary>Сервер → клиенты рядом: анимация или наложение эмоции.</summary>
[Serializable, NetSerializable]
public sealed class ImperialEmoteVisualMessage(NetEntity entity, ImperialEmoteEffect effect) : EntityEventArgs
{
    public readonly NetEntity Entity = entity;
    public readonly ImperialEmoteEffect Effect = effect;
}
