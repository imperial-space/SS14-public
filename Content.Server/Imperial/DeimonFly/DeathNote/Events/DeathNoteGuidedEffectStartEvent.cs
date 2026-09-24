namespace Content.Server.Imperial.DeimonFly.DeathNote.Events;

/// <summary>
/// Запрашивает подготовку журнала и политики необратимости непосредственно перед запуском
/// механической части направляемого сценария.
/// </summary>
[ByRefEvent]
public record struct DeathNoteGuidedEffectStartEvent(uint EntryId)
{
    /// <summary>
    /// Устанавливается <see cref="Systems.DeathNoteSystem"/> только после включения отслеживания
    /// записи и применения её политики необратимости.
    /// </summary>
    public bool Prepared;
}
