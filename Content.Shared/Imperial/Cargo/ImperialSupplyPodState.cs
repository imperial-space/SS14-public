using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

[Serializable, NetSerializable]
public enum ImperialSupplyPodState : byte
{
    Falling,
    Closed,
    Open,
}
