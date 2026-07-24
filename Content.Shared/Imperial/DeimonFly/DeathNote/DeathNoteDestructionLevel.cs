using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.DeimonFly.DeathNote;

/// <summary>
/// Ожидаемый масштаб побочного ущерба пресета.
/// </summary>
[Serializable, NetSerializable]
public enum DeathNoteDestructionLevel : byte
{
    Minimal,
    Low,
    Moderate,
    High,
    Extreme,
}
