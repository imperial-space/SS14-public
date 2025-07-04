namespace Content.Shared.Imperial.LeaveNoTrace;


public sealed partial class NinjaRevealedAttemptEvent(EntityUid ninja, EntityUid performer) : CancellableEntityEventArgs
{
    public EntityUid Ninja = ninja;

    public EntityUid Performer = performer;
}
