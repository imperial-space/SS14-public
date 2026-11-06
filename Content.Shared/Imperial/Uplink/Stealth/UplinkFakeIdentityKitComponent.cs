using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Набор поддельной личности (fake_identity_kit).</summary>
[RegisterComponent]
public sealed partial class UplinkFakeIdentityKitComponent : Component
{
    [DataField]
    public ProtoId<JobPrototype> Job = "Passenger";

    [DataField]
    public EntProtoId IdCard = "PassengerIDCard";
}
