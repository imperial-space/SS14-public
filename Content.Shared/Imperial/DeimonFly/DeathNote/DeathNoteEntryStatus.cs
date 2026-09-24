using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.DeimonFly.DeathNote;

/// <summary>
/// Серверное состояние записи. Обычный интерфейс тетради его не получает.
/// </summary>
[Serializable, NetSerializable]
public enum DeathNoteEntryStatus : byte
{
    Submitted,
    Scheduled,
    CustomDelivered,
    Executing,
    EffectStarted,
    DeathConfirmed,
    Failed,
    TargetMissing,
    TargetAlreadyDead,
    AmbiguousName,
    InvalidFormat,
    Cancelled,
}
