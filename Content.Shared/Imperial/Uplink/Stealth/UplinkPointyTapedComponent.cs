using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Предмет обмотан острой лентой.</summary>
[RegisterComponent]
public sealed partial class UplinkPointyTapedComponent : Component
{
    [DataField]
    public float Damage = 10f;
}
