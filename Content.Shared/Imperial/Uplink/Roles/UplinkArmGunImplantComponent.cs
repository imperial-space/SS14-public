using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Лазерная рука: имплант с лазерным пистолетом в руке.</summary>
[RegisterComponent]
public sealed partial class UplinkArmGunImplantComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Gun;

    [ViewVariables]
    public EntityUid? Spawned;
}
