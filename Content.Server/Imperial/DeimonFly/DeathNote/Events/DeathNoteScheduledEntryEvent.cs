namespace Content.Server.Imperial.DeimonFly.DeathNote.Events;

/// <summary>
/// Одноразовый сигнал планировщика. ID раунда защищает от обработчика предыдущей смены.
/// </summary>
public sealed class DeathNoteScheduledEntryEvent : EntityEventArgs
{
    public uint EntryId { get; }
    public DeathNoteScheduledPhase Phase { get; }
    public int RoundId { get; }

    public DeathNoteScheduledEntryEvent(uint entryId, DeathNoteScheduledPhase phase, int roundId)
    {
        EntryId = entryId;
        Phase = phase;
        RoundId = roundId;
    }
}
