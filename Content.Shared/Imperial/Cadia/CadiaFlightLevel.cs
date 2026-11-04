using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Высота этажа стопки Кадии для шаттлов: земля, небо, орбита.</summary>
[Serializable, NetSerializable]
public enum CadiaFlightLevel : byte
{
    Surface,
    Air,
    Orbit,
}
