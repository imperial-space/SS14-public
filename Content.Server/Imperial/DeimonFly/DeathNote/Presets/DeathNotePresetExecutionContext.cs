namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Минимальный серверный контекст запуска обработчика.
/// Актуальные координаты обработчик получает по Target во время исполнения.
/// </summary>
public readonly record struct DeathNotePresetExecutionContext(
    uint EntryId,
    EntityUid Notebook,
    EntityUid? Owner,
    EntityUid Writer,
    EntityUid Target,
    string EnteredTargetName = "");
