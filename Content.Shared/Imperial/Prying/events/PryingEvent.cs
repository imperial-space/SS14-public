namespace Content.Shared.Imperial.Prying.Events;


public sealed class PryingEvent(EntityUid user) : EventArgs
{
    public EntityUid User = user;
}
