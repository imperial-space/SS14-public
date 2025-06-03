using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.LeaveNoTrace;

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
