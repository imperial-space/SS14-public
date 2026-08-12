using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.DeimonFly.DeathNote;

/// <summary>
/// Безопасный ответ на попытку создать запись.
/// Ошибки поиска цели намеренно не представлены отдельными значениями.
/// </summary>
[Serializable, NetSerializable]
public enum DeathNoteSubmissionFeedback : byte
{
    None,
    Accepted,
    InvalidFormat,
    InvalidName,
    NameTooLong,
    CauseTooLong,
    InvalidTime,
    TimeInPast,
    TimeTooSoon,
    UnknownPreset,
    InvalidPage,
    PageFull,
    EntryLimitReached,
    Cooldown,
    NoPen,
    AccessDenied,
    Replay,
    TechnicalError,
}
