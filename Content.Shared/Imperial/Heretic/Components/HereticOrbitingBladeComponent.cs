using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Marker on a decorative knife entity that orbits a heretic (Mark of the Blade / Furious Steel).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HereticOrbitingBladeComponent : Component
{
    [DataField]
    public EntityUid OwnerHeretic = EntityUid.Invalid;
}
