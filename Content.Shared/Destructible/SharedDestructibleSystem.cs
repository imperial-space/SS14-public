namespace Content.Shared.Destructible;

public abstract class SharedDestructibleSystem : EntitySystem
{
    /// <summary>
    ///     Force entity to be destroyed and deleted.
    /// </summary>
    public bool DestroyEntity(EntityUid owner, EntityUid? performer = null)
    {
        var ev = new DestructionAttemptEvent();
        RaiseLocalEvent(owner, ev);
        if (ev.Cancelled)
            return false;

        var eventArgs = new DestructionEventArgs(performer);
        RaiseLocalEvent(owner, eventArgs);

        QueueDel(owner);
        return true;
    }

    /// <summary>
    ///     Force entity to break.
    /// </summary>
    public void BreakEntity(EntityUid owner, EntityUid? performer = null)
    {
        var eventArgs = new BreakageEventArgs(performer);
        RaiseLocalEvent(owner, eventArgs);
    }
}

/// <summary>
///     Raised before an entity is about to be destroyed and deleted
/// </summary>
public sealed class DestructionAttemptEvent : CancellableEntityEventArgs
{

}

/// <summary>
///     Raised when entity is destroyed and about to be deleted.
/// </summary>
public sealed class DestructionEventArgs : EntityEventArgs
{
    public readonly EntityUid? Performer;

    public DestructionEventArgs(EntityUid? performer = null)
    {
        Performer = performer;
    }
}

/// <summary>
///     Raised when entity was heavy damage and about to break.
/// </summary>
public sealed class BreakageEventArgs : EntityEventArgs
{
    public readonly EntityUid? Performer;

    public BreakageEventArgs(EntityUid? performer = null)
    {
        Performer = performer;
    }
}
