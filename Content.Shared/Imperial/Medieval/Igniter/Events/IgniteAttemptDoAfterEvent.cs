using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Igniter;


[Serializable, NetSerializable]
public sealed partial class IgniteAttemptDoAfterEvent : SimpleDoAfterEvent
{
    public NetEntity Performer;
    public NetEntity IgniteTarget;

    public IgniteAttemptDoAfterEvent(NetEntity performer, NetEntity target)
    {
        Performer = performer;
        IgniteTarget = target;
    }
}
