namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Серверный ограничитель повторных записей о несанкционированном доступе к журналу.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteAdminLedgerRuntimeComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextInvalidAttemptLogTime;
}
