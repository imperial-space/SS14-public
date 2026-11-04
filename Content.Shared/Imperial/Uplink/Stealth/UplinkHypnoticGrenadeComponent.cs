using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Гипнотическая граната: все в радиусе впадают в транс.</summary>
[RegisterComponent]
public sealed partial class UplinkHypnoticGrenadeComponent : Component
{
    [DataField]
    public float Range = 7f;
}
