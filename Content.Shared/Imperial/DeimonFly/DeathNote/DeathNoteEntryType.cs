using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.DeimonFly.DeathNote;

/// <summary>
/// Категория подтверждённой записи в Тетради смерти.
/// </summary>
[Serializable, NetSerializable]
public enum DeathNoteEntryType : byte
{
    Standard,
    Preset,
    Custom,
}
