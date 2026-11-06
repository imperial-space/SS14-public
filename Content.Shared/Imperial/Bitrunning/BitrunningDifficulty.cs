using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>Сложность домена (BITRUNNER_DIFFICULTY_*).</summary>
[Serializable, NetSerializable]
public enum BitrunningDifficulty : byte
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
}
