using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.DeimonFly.DeathNote.Models;

/// <summary>
/// Безопасная неизменяемая строка для обычного интерфейса тетради.
/// </summary>
[Serializable, NetSerializable]
public sealed record DeathNotePublicEntry(
    int PageIndex,
    int LineIndex,
    string OriginalText);
