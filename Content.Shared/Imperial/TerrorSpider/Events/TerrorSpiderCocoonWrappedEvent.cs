namespace Content.Shared.Imperial.TerrorSpider.Events;

public sealed partial class TerrorSpiderCocoonWrappedEvent : EntityEventArgs
{
    public EntityUid User;
    public EntityUid Target;
    public EntityUid Cocoon;

    public TerrorSpiderCocoonWrappedEvent(EntityUid user, EntityUid target, EntityUid cocoon)
    {
        User = user;
        Target = target;
        Cocoon = cocoon;
    }
}
