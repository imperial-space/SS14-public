namespace Content.Server.Imperial.DeimonFly.DeathNote.Events;

/// <summary>
/// Запланированная запись может предупредить цель до точного момента исполнения.
/// </summary>
public enum DeathNoteScheduledPhase : byte
{
    Prelude,
    Execution,
}
