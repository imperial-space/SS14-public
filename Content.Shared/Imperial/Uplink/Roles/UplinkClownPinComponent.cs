using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Клоунский боёк (firing_pin/clown/ultra): стрелять могут только клоуны и неуклюжие.</summary>
[RegisterComponent]
public sealed partial class UplinkClownPinComponent : Component
{
    /// <summary>selfdestruct: оружие взрывается у не-клоуна.</summary>
    [DataField]
    public bool SelfDestruct;
}
