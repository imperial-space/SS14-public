using System.Globalization;
using System.Text.RegularExpressions;

namespace Content.Shared.Imperial.DeimonFly.DeathNote.Parsing;

/// <summary>
/// Чистый разбор недоверенной строки тетради. Методы не зависят от ECS и подходят для unit-тестов.
/// </summary>
public static class DeathNoteInputParser
{
    public const string RoundTimeFormat = @"hh\:mm\:ss";
    private const string RussianDeathVerb = @"(?:умр(?:е|ё)т|погибнет|скончается)";

    private static readonly Regex RussianPresetLine = new(
        $@"^(?<name>.+?)\s+в\s+(?<time>\d{{2}}:\d{{2}}:\d{{2}})\s+{RussianDeathVerb}\s+от\s+(?<cause>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RussianPresetLineWithoutTime = new(
        $@"^(?<name>.+?)\s+{RussianDeathVerb}\s+от\s+(?<cause>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RussianCustomLine = new(
        @"^(?<name>.+?)\s+в\s+(?<time>\d{2}:\d{2}:\d{2})\s+исполнит\s*:\s*(?<custom>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RussianStandardLine = new(
        $@"^(?<name>.+?)\s+в\s+(?<time>\d{{2}}:\d{{2}}:\d{{2}})\s+{RussianDeathVerb}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RussianStandardLineWithoutTime = new(
        $@"^(?<name>.+?)\s+{RussianDeathVerb}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LeadingBangCustomLine = new(
        @"^!(?<name>.+?)\s+(?:в|at)\s+(?<time>\d{2}:\d{2}:\d{2})\s+(?<scenario>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BangAfterTimeCustomLine = new(
        @"^(?<name>.+?)\s+(?:в|at)\s+(?<time>\d{2}:\d{2}:\d{2})\s+!(?<scenario>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BangAfterNameCustomLine = new(
        @"^(?<name>.+?)\s+!(?<scenario>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ScenarioTimePrefix = new(
        @"^(?:в|at)\s+(?<time>\d{2}:\d{2}:\d{2})(?:\s+|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EnglishPresetLine = new(
        @"^(?<name>.+?)\s+at\s+(?<time>\d{2}:\d{2}:\d{2})\s+shall\s+die\s+from\s+(?<cause>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EnglishPresetLineWithoutTime = new(
        @"^(?<name>.+?)\s+shall\s+die\s+from\s+(?<cause>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EnglishCustomLine = new(
        @"^(?<name>.+?)\s+at\s+(?<time>\d{2}:\d{2}:\d{2})\s+shall\s+fulfill\s*:\s*(?<custom>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EnglishStandardLine = new(
        @"^(?<name>.+?)\s+at\s+(?<time>\d{2}:\d{2}:\d{2})\s+shall\s+die$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EnglishStandardLineWithoutTime = new(
        @"^(?<name>.+?)\s+shall\s+die$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string NormalizeName(string? input)
    {
        return input?.Trim() ?? string.Empty;
    }

    public static string NormalizeCause(string? input)
    {
        return (input?.Trim() ?? string.Empty)
            .Replace('Ё', 'Е')
            .Replace('ё', 'е');
    }

    /// <summary>
    /// Возвращает точный ролевой текст, который останется на странице.
    /// Восклицательный знак между истинным именем и сценарием служит синтаксисом
    /// и не входит в отображаемую запись.
    /// </summary>
    public static string NormalizeWrittenText(string? input)
    {
        var text = input?.Trim() ?? string.Empty;
        return TryMatchBangCustom(text, out _, out var visibleText)
            ? visibleText
            : text;
    }

    /// <summary>
    /// Принимает только строгое HH:MM:SS. Формат TimeSpan ограничивает часы диапазоном 00-23.
    /// </summary>
    public static bool TryParseRoundTime(string? input, out TimeSpan time)
    {
        return TimeSpan.TryParseExact(
            input?.Trim(),
            RoundTimeFormat,
            CultureInfo.InvariantCulture,
            out time);
    }

    public static bool TryParseCause(string? input, out DeathNoteParsedCause cause)
    {
        var trimmed = input?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            cause = new DeathNoteParsedCause(DeathNoteEntryType.Standard, string.Empty, null);
            return true;
        }

        if (trimmed[0] != '!')
        {
            var normalized = NormalizeCause(trimmed);
            cause = new DeathNoteParsedCause(DeathNoteEntryType.Preset, normalized, null);
            return true;
        }

        // В ролевом предписании регистр и «ё» являются частью авторского текста.
        // Нормализация е/ё нужна только для поиска пресета, а не для отображения жертве.
        var customText = trimmed[1..].Trim();
        if (customText.Length == 0)
        {
            cause = default;
            return false;
        }

        cause = new DeathNoteParsedCause(DeathNoteEntryType.Custom, trimmed, customText);
        return true;
    }

    /// <summary>
    /// Принимает только законченную ролевую фразу. Окончательная точка необязательна,
    /// внутренние пробелы имени и причины сохраняются.
    /// </summary>
    public static bool TryParseLine(string? input, out DeathNoteParsedLine line)
    {
        var original = input?.Trim() ?? string.Empty;
        if (original.Length == 0)
        {
            line = default;
            return false;
        }

        // Разделитель после истинного имени делает поиск цели однозначным,
        // а жертва всё равно получает естественную фразу без служебного символа.
        if (TryMatchBangCustom(original, out line, out _))
        {
            return true;
        }

        // При наличии «!» он считается синтаксисом, а не пунктуацией.
        // Некорректное особое указание нельзя незаметно принять за одно имя.
        if (original.Contains('!'))
        {
            line = default;
            return false;
        }

        var normalized = original;
        normalized = normalized.TrimEnd('.', '!', ' ');
        if (TryMatchPreset(RussianPresetLine, normalized, out line) ||
            TryMatchPreset(EnglishPresetLine, normalized, out line) ||
            TryMatchPresetWithoutTime(RussianPresetLineWithoutTime, normalized, out line) ||
            TryMatchPresetWithoutTime(EnglishPresetLineWithoutTime, normalized, out line) ||
            TryMatchCustom(RussianCustomLine, normalized, out line) ||
            TryMatchCustom(EnglishCustomLine, normalized, out line) ||
            TryMatchStandard(RussianStandardLine, normalized, out line) ||
            TryMatchStandard(EnglishStandardLine, normalized, out line) ||
            TryMatchStandardWithoutTime(RussianStandardLineWithoutTime, normalized, out line) ||
            TryMatchStandardWithoutTime(EnglishStandardLineWithoutTime, normalized, out line))
        {
            return true;
        }

        // Одно истинное имя следует исходному правилу сорока секунд: без времени и причины.
        var bareName = NormalizeName(normalized);
        if (bareName.Length > 0 &&
            !bareName.StartsWith('!') &&
            !bareName.Contains('!') &&
            !bareName.Contains(':') &&
            !bareName.Contains('|'))
        {
            line = new DeathNoteParsedLine(bareName, string.Empty, string.Empty);
            return true;
        }

        line = default;
        return false;
    }

    private static bool TryMatchPreset(Regex regex, string input, out DeathNoteParsedLine line)
    {
        var match = regex.Match(input);
        if (!match.Success)
        {
            line = default;
            return false;
        }

        var name = NormalizeName(match.Groups["name"].Value);
        var time = match.Groups["time"].Value;
        var cause = NormalizeCause(match.Groups["cause"].Value);
        if (name.Length == 0 || cause.Length == 0 || !TryParseRoundTime(time, out _))
        {
            line = default;
            return false;
        }

        line = new DeathNoteParsedLine(name, time, cause);
        return true;
    }

    private static bool TryMatchCustom(Regex regex, string input, out DeathNoteParsedLine line)
    {
        var match = regex.Match(input);
        if (!match.Success)
        {
            line = default;
            return false;
        }

        var name = NormalizeName(match.Groups["name"].Value);
        var time = match.Groups["time"].Value;
        var custom = NormalizeCause(match.Groups["custom"].Value);
        if (name.Length == 0 || custom.Length == 0 || !TryParseRoundTime(time, out _))
        {
            line = default;
            return false;
        }

        line = new DeathNoteParsedLine(name, time, $"! {custom}");
        return true;
    }

    private static bool TryMatchBangCustom(
        string input,
        out DeathNoteParsedLine line,
        out string visibleText)
    {
        var leadingMatch = LeadingBangCustomLine.Match(input);
        if (leadingMatch.Success)
        {
            var name = NormalizeName(leadingMatch.Groups["name"].Value);
            var time = leadingMatch.Groups["time"].Value;
            visibleText = input[1..].TrimStart();
            return TryCreateCustomLine(name, time, visibleText, out line);
        }

        var afterTimeMatch = BangAfterTimeCustomLine.Match(input);
        if (afterTimeMatch.Success)
        {
            var name = NormalizeName(afterTimeMatch.Groups["name"].Value);
            var time = afterTimeMatch.Groups["time"].Value;
            visibleText = RemoveControlBang(input);
            return TryCreateCustomLine(name, time, visibleText, out line);
        }

        var afterNameMatch = BangAfterNameCustomLine.Match(input);
        if (afterNameMatch.Success)
        {
            var name = NormalizeName(afterNameMatch.Groups["name"].Value);
            var scenario = NormalizeCause(afterNameMatch.Groups["scenario"].Value);
            var timeMatch = ScenarioTimePrefix.Match(scenario);
            var time = timeMatch.Success
                ? timeMatch.Groups["time"].Value
                : string.Empty;
            visibleText = RemoveControlBang(input);
            return TryCreateCustomLine(name, time, visibleText, out line);
        }

        line = default;
        visibleText = input;
        return false;
    }

    private static bool TryCreateCustomLine(
        string name,
        string time,
        string visibleText,
        out DeathNoteParsedLine line)
    {
        if (name.Length == 0 ||
            visibleText.Length == 0 ||
            time.Length > 0 && !TryParseRoundTime(time, out _))
        {
            line = default;
            return false;
        }

        line = new DeathNoteParsedLine(name, time, $"! {visibleText}");
        return true;
    }

    private static string RemoveControlBang(string input)
    {
        var bangIndex = input.IndexOf('!');
        if (bangIndex < 0)
            return input.Trim();

        var before = input[..bangIndex].TrimEnd();
        var after = input[(bangIndex + 1)..].TrimStart();
        if (before.Length == 0)
            return after;
        if (after.Length == 0)
            return before;

        return $"{before} {after}";
    }

    private static bool TryMatchPresetWithoutTime(Regex regex, string input, out DeathNoteParsedLine line)
    {
        var match = regex.Match(input);
        if (!match.Success)
        {
            line = default;
            return false;
        }

        var name = NormalizeName(match.Groups["name"].Value);
        var cause = NormalizeCause(match.Groups["cause"].Value);
        if (name.Length == 0 || cause.Length == 0)
        {
            line = default;
            return false;
        }

        line = new DeathNoteParsedLine(name, string.Empty, cause);
        return true;
    }

    private static bool TryMatchStandard(Regex regex, string input, out DeathNoteParsedLine line)
    {
        var match = regex.Match(input);
        if (!match.Success)
        {
            line = default;
            return false;
        }

        var name = NormalizeName(match.Groups["name"].Value);
        var time = match.Groups["time"].Value;
        if (name.Length == 0 || !TryParseRoundTime(time, out _))
        {
            line = default;
            return false;
        }

        line = new DeathNoteParsedLine(name, time, string.Empty);
        return true;
    }

    private static bool TryMatchStandardWithoutTime(Regex regex, string input, out DeathNoteParsedLine line)
    {
        var match = regex.Match(input);
        if (!match.Success)
        {
            line = default;
            return false;
        }

        var name = NormalizeName(match.Groups["name"].Value);
        if (name.Length == 0)
        {
            line = default;
            return false;
        }

        line = new DeathNoteParsedLine(name, string.Empty, string.Empty);
        return true;
    }
}
