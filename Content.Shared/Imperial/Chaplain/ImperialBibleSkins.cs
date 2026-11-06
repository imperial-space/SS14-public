namespace Content.Shared.Imperial.Chaplain;

/// <summary>
/// Обложки библии из SS13 (GLOB.biblenames / biblestates / bibleitemstates).
/// </summary>
public static class ImperialBibleSkins
{
    /// <summary>Максимальная длина названий религии, божества и библии (MAX_NAME_LEN).</summary>
    public const int MaxNameLength = 42;

    /// <summary>
    /// Состояние в RSI библии и префикс спрайта в руках.
    /// </summary>
    public static readonly (string State, string HeldPrefix)[] All =
    {
        ("bible", "bible"),
        ("koran", "koran"),
        ("scrapbook", "scrapbook"),
        ("burning", "bible"),
        ("honk1", "bible"),
        ("honk2", "bible"),
        ("creeper", "bible"),
        ("white", "bible"),
        ("holylight", "bible"),
        ("atheist", "bible"),
        ("tome", "bible"),
        ("kingyellow", "kingyellow"),
        ("ithaqua", "ithaqua"),
        ("scientology", "scientology"),
        ("melted", "melted"),
        ("necronomicon", "necronomicon"),
        ("insuls", "kingyellow"),
        ("gurugranthsahib", "bible"),
        ("kojiki", "kojiki"),
    };

    public static bool TryGetHeldPrefix(string state, out string heldPrefix)
    {
        foreach (var (skin, prefix) in All)
        {
            if (skin != state)
                continue;

            heldPrefix = prefix;
            return true;
        }

        heldPrefix = string.Empty;
        return false;
    }
}
