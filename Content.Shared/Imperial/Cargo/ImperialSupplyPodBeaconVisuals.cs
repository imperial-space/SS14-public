using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

[Serializable, NetSerializable]
public enum ImperialSupplyPodBeaconVisuals : byte
{
    /// <summary>"", sp_orange, sp_yellow, sp_green.</summary>
    Light,
}
