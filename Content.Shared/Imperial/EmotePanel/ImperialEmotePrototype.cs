using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EmotePanel;

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

    /// <summary>Предмет «рука» (hand_item), который эмоция кладёт в свободную руку.</summary>
    [DataField]
    public EntProtoId? HandItem;

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

    /// <summary>Эмоция что-то делает: кладёт руку, анимирует, сбивает с ног, меняет экраны.</summary>
    public bool IsAction => Effect != ImperialEmoteEffect.None || HandItem != null;
}
