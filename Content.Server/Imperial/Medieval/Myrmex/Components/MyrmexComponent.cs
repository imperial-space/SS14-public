using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.Myrmex.Components;

[RegisterComponent]
public sealed partial class MyrmexComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField(customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
    public List<string> Actions;

    //since multiple actions can modify speed we have to track the total speed percentage
    public float CurrentSpeedMultiplier = 1;

    public bool StealthActive;

    public bool StunActive;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public TimeSpan StunDuration;

    public bool ArmorActive;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public ProtoId<DamageModifierSetPrototype> StandardArmorProto; //PrototypeIdSerializer<DamageModifierSetPrototype> would be nice but it isn't implemented

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public ProtoId<DamageModifierSetPrototype> ActiveArmorProto;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float ActiveArmorSpeedMultiplier;
}
