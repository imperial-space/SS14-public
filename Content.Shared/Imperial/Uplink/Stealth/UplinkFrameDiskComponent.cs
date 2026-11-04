using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Диск F.R.A.M.E.: превращает чужой КПК в аплинк без ТК.</summary>
[RegisterComponent]
public sealed partial class UplinkFrameDiskComponent : Component
{
    [DataField]
    public int Charges = 5;
}
