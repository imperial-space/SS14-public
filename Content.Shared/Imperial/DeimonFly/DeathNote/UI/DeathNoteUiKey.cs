using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.DeimonFly.DeathNote.UI;

/// <summary>
/// Ключи отдельных интерфейсов обычной и административной тетрадей.
/// </summary>
[Serializable, NetSerializable]
public enum DeathNoteUiKey : byte
{
    Notebook,
    AdminLedger,
}
