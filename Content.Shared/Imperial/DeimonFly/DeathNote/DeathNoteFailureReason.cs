using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.DeimonFly.DeathNote;

/// <summary>
/// Точная причина неудачи для серверного и административного журнала.
/// </summary>
[Serializable, NetSerializable]
public enum DeathNoteFailureReason : byte
{
    None,
    TargetMissing,
    TargetAlreadyDead,
    TargetDeleted,
    TargetNoLongerAlive,
    AmbiguousName,
    InvalidFormat,
    InvalidName,
    InvalidTime,
    TimeInPast,
    TimeTooSoon,
    UnknownPreset,
    NameTooLong,
    CauseTooLong,
    InvalidPage,
    PageFull,
    EntryLimitReached,
    Cooldown,
    Replay,
    NoPen,
    AccessDenied,
    HandlerError,
    RoundEnded,
    TechnicalError,
}
