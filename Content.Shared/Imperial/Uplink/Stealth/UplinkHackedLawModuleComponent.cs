using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Взломанный модуль законов ИИ.</summary>
[RegisterComponent]
public sealed partial class UplinkHackedLawModuleComponent : Component
{
    [DataField]
    public string Law = string.Empty;
}
