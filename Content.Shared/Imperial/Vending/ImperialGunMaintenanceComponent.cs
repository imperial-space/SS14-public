using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>gun_maintenance_supplies TG: чинит оружие, тратит заряд.</summary>
[RegisterComponent]
public sealed partial class ImperialGunMaintenanceComponent : Component
{
    [DataField]
    public int Uses = 3;

    [DataField]
    public int MaxUses = 3;
}
