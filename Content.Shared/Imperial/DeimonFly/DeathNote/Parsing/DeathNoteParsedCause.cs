namespace Content.Shared.Imperial.DeimonFly.DeathNote.Parsing;

/// <summary>
/// Результат чистого разбора причины до обращения к серверному реестру пресетов.
/// </summary>
public readonly record struct DeathNoteParsedCause(
    DeathNoteEntryType EntryType,
    string CauseText,
    string? CustomText);
