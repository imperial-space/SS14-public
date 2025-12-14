using Robust.Shared.GameStates;

//=========================================================================
// GrabberComponent.cs
//=========================================================================
// Purpose: Allows an entity to grab others
// Author: rhailrake
//=========================================================================

namespace Content.Shared.Imperial.Medieval.Grab.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(true, true)]
public sealed partial class GrabberComponent : Component
{
    /// <summary>
    /// Entity currently being grabbed.
    /// </summary>
    [AutoNetworkedField, DataField]
    public EntityUid? GrabbedEntity;

    /// <summary>
    /// Range.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public float GrabRange = 1f;

    /// <summary>
    /// Applies movement modifier while grabbing.
    /// </summary>
    public float WalkSpeedModifier => GrabbedEntity == default ? 1.0f : 0.9f;
    public float SprintSpeedModifier => GrabbedEntity == default ? 1.0f : 0.9f;
}
