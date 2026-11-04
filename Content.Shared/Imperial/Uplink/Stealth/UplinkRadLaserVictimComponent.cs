using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Отложенный эффект микролазера на цели.</summary>
[RegisterComponent]
public sealed partial class UplinkRadLaserVictimComponent : Component
{
    [ViewVariables]
    public TimeSpan TriggerAt;

    [ViewVariables]
    public int Intensity;
}
