using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>TRAIT_SYNDIE_KISS.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndieKisserComponent : Component
{
    [DataField]
    public EntProtoId Kisser = "ImperialSyndieKissBlower";
}
