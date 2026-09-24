namespace Content.Server.Imperial.DeimonFly.DeathNote.Events;

/// <summary>
/// Сообщает основной системе, какие ожидающие записи отменены окончанием раунда.
/// </summary>
public sealed class DeathNoteTimersCancelledEvent : EntityEventArgs
{
    public IReadOnlyList<uint> EntryIds { get; }

    public DeathNoteTimersCancelledEvent(IReadOnlyList<uint> entryIds)
    {
        EntryIds = entryIds;
    }
}
