using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Злой росток: семена, прорастающие в злое растение-союзника.</summary>
[RegisterComponent]
public sealed partial class UplinkEvilSeedComponent : Component
{
    [DataField]
    public EntProtoId Mob = "ImperialEvilSeedling";

    [DataField]
    public TimeSpan GrowTime = TimeSpan.FromSeconds(30);

    [ViewVariables]
    public TimeSpan? GrowAt;
}
