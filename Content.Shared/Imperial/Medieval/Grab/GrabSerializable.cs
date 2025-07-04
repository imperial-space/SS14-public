using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

//========================================================================
// GrabSerializable.cs
//========================================================================
// Purpose: Defines the event messaging system for the grab mechanic
// Author: rhailrake
//========================================================================

namespace Content.Shared.Imperial.Medieval.Grab;

//========================================================================
// BASE MESSAGE CLASSES
//========================================================================

/// <summary>
/// Base class for all grab-related messages
/// Provides common grabber/grabbed entity references for all grab events
/// </summary>
/// <param name="grabberUid">The entity attempting to grab</param>
/// <param name="grabbedUid">The entity being grabbed</param>
public abstract class GrabMessage(EntityUid? grabberUid, EntityUid grabbedUid) : EntityEventArgs
{
    /// <summary>
    /// The entity that is performing the grab action
    /// </summary>
    public readonly EntityUid? GrabberUid = grabberUid;

    /// <summary>
    /// The entity that is being grabbed
    /// </summary>
    public readonly EntityUid GrabbedUid = grabbedUid;
}

/// <summary>
/// Base class for grab messages that can be cancelled by event handlers
/// Extends GrabMessage with cancellation capability for prevention logic
/// </summary>
/// <param name="grabberUid">The entity attempting to grab</param>
/// <param name="grabbedUid">The entity being grabbed</param>
public abstract class CancellableGrabMessage(EntityUid? grabberUid, EntityUid grabbedUid) : CancellableEntityEventArgs
{
    /// <summary>
    /// The entity that is performing the grab action
    /// </summary>
    public readonly EntityUid? GrabberUid = grabberUid;

    /// <summary>
    /// The entity that is being grabbed
    /// </summary>
    public readonly EntityUid GrabbedUid = grabbedUid;
}


//========================================================================
// GRAB ATTEMPT EVENTS
//========================================================================

/// <summary>
/// Fired when an entity attempts to grab another entity
/// This is the main validation event - cancel to prevent the grab
/// </summary>
public sealed class GrabAttemptEvent(EntityUid? grabberUid, EntityUid grabbedUid) : CancellableGrabMessage(grabberUid, grabbedUid);

/// <summary>
/// Fired when a grab attempt is initiated
/// </summary>
public sealed class StartGrabAttemptEvent(EntityUid? grabberUid, EntityUid grabbedUid) : CancellableGrabMessage(grabberUid, grabbedUid);

/// <summary>
/// Fired when a grab attempt is being stopped/cancelled
/// </summary>
public sealed class StopGrabAttemptEvent(EntityUid? grabberUid, EntityUid grabbedUid) : CancellableGrabMessage(grabberUid, grabbedUid);

/// <summary>
/// Fired on the entity being grabbed to allow it to resist or prevent the grab
/// </summary>
public sealed class BeingGrabbedAttemptEvent(EntityUid? grabberUid, EntityUid grabbedUid) : CancellableGrabMessage(grabberUid, grabbedUid);


//========================================================================
// GRAB STATE EVENTS
//========================================================================

/// <summary>
/// Fired when a grab has successfully started
/// </summary>
public sealed class GrabStartedEvent(EntityUid? grabberUid, EntityUid grabbedUid) : GrabMessage(grabberUid, grabbedUid);

/// <summary>
/// Fired when an active grab has been stopped/released
/// </summary>
public sealed class GrabStoppedEvent(EntityUid? grabberUid, EntityUid grabbedUid) : GrabMessage(grabberUid, grabbedUid);

//========================================================================
// GRAB DOAFTERS
//========================================================================

[Serializable, NetSerializable]
public sealed partial class GrabDoAfterEvent : DoAfterEvent
{
    public NetEntity Grabber;
    public int Chance;

    public GrabDoAfterEvent(NetEntity target, int chance)
    {
        Grabber = target;
        Chance = chance;
    }

    public override DoAfterEvent Clone() => this;
}


[Serializable, NetSerializable]
public sealed partial class GrabEscapeDoAfterEvent : DoAfterEvent
{
    public NetEntity Grabber;
    public int Chance;

    public GrabEscapeDoAfterEvent(NetEntity target, int chance)
    {
        Grabber = target;
        Chance = chance;
    }

    public override DoAfterEvent Clone() => this;
}



//========================================================================
// GRAB ALERTS
//========================================================================

public sealed partial class StopGrabbingAlertEvent : BaseAlertEvent;

public sealed partial class EscapeGrabbingAlertEvent : BaseAlertEvent;
