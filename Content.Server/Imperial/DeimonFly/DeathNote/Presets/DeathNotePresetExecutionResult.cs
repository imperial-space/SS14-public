namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Результат запуска штатной механики. Успех не гарантирует фактическую смерть цели.
/// </summary>
public readonly record struct DeathNotePresetExecutionResult(bool Success, string? Result, string? Error)
{
    public static DeathNotePresetExecutionResult Succeeded(string? result = null)
    {
        return new DeathNotePresetExecutionResult(true, result, null);
    }

    public static DeathNotePresetExecutionResult Failed(string error)
    {
        return new DeathNotePresetExecutionResult(false, null, error);
    }
}
