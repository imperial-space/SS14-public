namespace Content.Shared.Imperial.DeimonFly.DeathNote;

/// <summary>
/// Разрешённые переходы серверной записи. Терминальные состояния нельзя запустить повторно.
/// </summary>
public static class DeathNoteStatusTransitions
{
    public static bool CanTransition(DeathNoteEntryStatus from, DeathNoteEntryStatus to)
    {
        return from switch
        {
            DeathNoteEntryStatus.Submitted => to is
                DeathNoteEntryStatus.Scheduled or
                DeathNoteEntryStatus.CustomDelivered or
                DeathNoteEntryStatus.Failed or
                DeathNoteEntryStatus.TargetMissing or
                DeathNoteEntryStatus.TargetAlreadyDead or
                DeathNoteEntryStatus.AmbiguousName or
                DeathNoteEntryStatus.InvalidFormat or
                DeathNoteEntryStatus.Cancelled,
            DeathNoteEntryStatus.Scheduled => to is
                DeathNoteEntryStatus.Executing or
                DeathNoteEntryStatus.CustomDelivered or
                DeathNoteEntryStatus.Failed or
                DeathNoteEntryStatus.TargetAlreadyDead or
                DeathNoteEntryStatus.Cancelled,
            DeathNoteEntryStatus.Executing => to is
                DeathNoteEntryStatus.EffectStarted or
                DeathNoteEntryStatus.DeathConfirmed or
                DeathNoteEntryStatus.Failed,
            DeathNoteEntryStatus.EffectStarted => to is DeathNoteEntryStatus.DeathConfirmed,
            _ => false,
        };
    }
}
