namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class SpecificEvent : BossAttackAction
{
    [DataField(required: true)]
    public BaseBossAttackEvent Event;

    [DataField]
    public bool Broadcast = false;

    public override void Execute(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
    {
        if (Broadcast)
        {
            entMan.EventBus.RaiseEvent(EventSource.Local, Event);
            return;
        }

        foreach (var target in targets)
        {
            entMan.EventBus.RaiseLocalEvent(target, Event);
        }
    }
}
