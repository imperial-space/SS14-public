using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

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
