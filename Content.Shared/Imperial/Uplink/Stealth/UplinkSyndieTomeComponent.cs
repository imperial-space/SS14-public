using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Том Синдиката.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndieTomeComponent : Component
{
    [DataField]
    public int Uses = 1;

    [ViewVariables]
    public string? OwnerName;
}
