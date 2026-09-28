using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Marker component present on all heretic blade weapons (inherited from BaseHereticBlade).
/// Used to subscribe melee-hit logic for any heretic blade, not just the path-upgrade blade.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HereticBladeWeaponComponent : Component { }
