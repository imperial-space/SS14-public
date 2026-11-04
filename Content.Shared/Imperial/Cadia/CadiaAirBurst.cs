using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Разрыв последних секунд на радаре: попал или нет.</summary>
[Serializable, NetSerializable]
public readonly record struct CadiaAirBurst(Vector2 Offset, int Level, bool Hit);
