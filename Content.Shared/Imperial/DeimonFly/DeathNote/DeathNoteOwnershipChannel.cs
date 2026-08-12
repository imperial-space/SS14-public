using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.DeimonFly.DeathNote;

/// <summary>
/// Разделяет владение и видимость богов смерти между независимыми наборами тетрадей.
/// </summary>
[Serializable, NetSerializable]
public enum DeathNoteOwnershipChannel : byte
{
    First,
    Second,
    Third,
}
