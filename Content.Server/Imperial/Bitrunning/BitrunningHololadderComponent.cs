using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>/obj/structure/hololadder: выход из домена.</summary>
[RegisterComponent]
public sealed partial class BitrunningHololadderComponent : Component
{
    /// <summary>travel_time.</summary>
    [DataField]
    public TimeSpan TravelTime = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public EntityUid? Server;
}
