using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.DeimonFly.DeathNote.Models;

/// <summary>
/// Расширенная запись для адресно отправляемой страницы административного журнала.
/// Содержит только строки-снимки, но не сетевые идентификаторы сущностей.
/// </summary>
[Serializable, NetSerializable]
public sealed record DeathNoteAdminEntry(
    uint EntryId,
    int PageIndex,
    int LineIndex,
    TimeSpan CreatedRoundTime,
    TimeSpan ScheduledRoundTime,
    string Notebook,
    string Owner,
    string Writer,
    string EnteredTargetName,
    string CauseText,
    DeathNoteEntryType EntryType,
    string PresetId,
    DeathNoteDestructionLevel? Destruction,
    string ResolvedTarget,
    DeathNoteEntryStatus Status,
    bool EffectStarted,
    bool DeathConfirmed,
    DeathNoteFailureReason FailureReason,
    string Result,
    string Error);
