using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>ДНК-инжектор, выпущенный из модифицированного шприцемёта.</summary>
[RegisterComponent]
public sealed partial class UplinkDnaDartComponent : Component
{
    [ViewVariables]
    public EntityUid? Shooter;
}
