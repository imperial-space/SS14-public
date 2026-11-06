using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

[Serializable, NetSerializable]
public enum ImperialExpressAction : byte
{
    LandingCargo,
    LandingBeacon,
    PrintBeacon,
}
