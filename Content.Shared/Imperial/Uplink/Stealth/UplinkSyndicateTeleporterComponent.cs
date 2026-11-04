using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Экспериментальный телепортер Синдиката.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndicateTeleporterComponent : Component
{
    [DataField]
    public int Charges = 4;

    [DataField]
    public int MaxCharges = 4;

    [DataField]
    public int MinDistance = 4;

    [DataField]
    public int MaxDistance = 8;

    [DataField]
    public int ParallelDistance = 3;

    [ViewVariables]
    public TimeSpan NextRechargeRoll;
}
