using Robust.Shared.GameStates;
using Content.Shared.FixedPoint;

namespace Content.Shared.Imperial.Heretic.Paths.Blade;

/// <summary>
/// Marker component present on all heretic blade weapons (inherited from BaseHereticBlade).
/// Used to subscribe melee-hit logic for any heretic blade, not just the path-upgrade blade.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HereticBladeWeaponComponent : Component
{
    [DataField]
    public float BladeBladeMeleeHitAmount = 10f;

    [DataField]
    public FixedPoint2 BladeBladeMeleeHitAmount2 = FixedPoint2.New(10);
}
