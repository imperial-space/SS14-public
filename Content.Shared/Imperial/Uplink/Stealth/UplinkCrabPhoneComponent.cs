using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Телефон «Протокол CRAB-17».</summary>
[RegisterComponent]
public sealed partial class UplinkCrabPhoneComponent : Component
{
    [DataField]
    public EntProtoId Machine = "ImperialCrab17Machine";
}
