using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

[Serializable, NetSerializable]
public enum ImperialSupplyPodVisuals : byte
{
    /// <summary>ImperialSupplyPodState.</summary>
    State,

    /// <summary>bool: обломки под капсулой.</summary>
    Rubble,
}
