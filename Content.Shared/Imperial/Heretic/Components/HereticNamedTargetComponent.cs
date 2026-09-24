using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Marks an entity as a named sacrifice target for one or more heretics.
/// Added by HereticSystem.AssignNamedTargets(); removed when the heretic is deleted
/// or targets are reassigned. Drives the HUD skull icon visible only to the owning heretic(s).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticNamedTargetComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<EntityUid> OwningHeretics = new();
}
