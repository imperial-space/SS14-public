using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Телепад в кейсе.</summary>
[RegisterComponent]
public sealed partial class UplinkLaunchpadComponent : Component
{
    [DataField]
    public int Range = 8;

    [DataField]
    public EntProtoId? Briefcase = "ImperialUplinkLaunchpadBriefcase";
}
