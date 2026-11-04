using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public enum ImperialFishMountVisuals : byte
{
    /// <summary>Прототип рыбы на подставке (пустая строка — нет рыбы).</summary>
    Fish,
}
