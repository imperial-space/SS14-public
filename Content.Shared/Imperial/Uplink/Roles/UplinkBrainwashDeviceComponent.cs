using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Программа «промывки мозгов»: внушает спящей жертве задачу.</summary>
[RegisterComponent]
public sealed partial class UplinkBrainwashDeviceComponent : Component
{
    [DataField]
    public TimeSpan BrainwashInteractDelay = TimeSpan.FromSeconds(10);
}
