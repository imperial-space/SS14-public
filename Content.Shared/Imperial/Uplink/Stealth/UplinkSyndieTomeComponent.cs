using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Том Синдиката.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndieTomeComponent : Component
{
    [DataField]
    public int Uses = 1;

    [ViewVariables]
    public string? OwnerName;

    [DataField]
    public DamageSpecifier TomeUseDamage = new() { DamageDict = { ["Piercing"] = 5 } };
}
