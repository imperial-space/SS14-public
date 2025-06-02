using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.LeaveNoTrace;

public abstract class SharedLeaveNoTraceSystem : EntitySystem
{
}

[Serializable, NetSerializable]
public sealed partial class LeaveNoTraceVisualEvent : EntityEventArgs
{
    public NetEntity Owner;
    public bool IsSeen;

    public LeaveNoTraceVisualEvent(NetEntity owner, bool isSeen)
    {
        Owner = owner;
        IsSeen = isSeen;
    }
}
