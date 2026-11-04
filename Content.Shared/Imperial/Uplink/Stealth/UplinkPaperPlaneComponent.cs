using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Бумажный самолётик: при попадании в глаза — размытие и паралич.</summary>
[RegisterComponent]
public sealed partial class UplinkPaperPlaneComponent : Component
{
    [DataField]
    public float HitProbability = 1f;
}
