using Robust.Shared.GameStates;

//=========================================================================
// GrabbableComponent.cs
//=========================================================================
// Purpose: Allows an entity to be grabbed by another
// Author: rhailrake
//=========================================================================

namespace Content.Shared.Imperial.Medieval.Grab.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrabbableComponent : Component
{
    /// <summary>
    /// Entity currently grabbing this one.
    /// </summary>
    [AutoNetworkedField, DataField]
    public EntityUid? Grabber;

    /// <summary>
    /// Joint used to link entities while grabbed.
    /// </summary>
    [AutoNetworkedField, DataField]
    public string? GrabJointId;

    /// <summary>
    /// Applies movement modifier while grabbed.
    /// </summary>
    public float WalkSpeedModifier => Grabber == default ? 1.0f : 0.75f;
    public float SprintSpeedModifier => Grabber == default ? 1.0f : 0.75f;

    [AutoNetworkedField]
    public bool DoAfterRaised = false;

    public int GrabMissStreak = 0;
}
