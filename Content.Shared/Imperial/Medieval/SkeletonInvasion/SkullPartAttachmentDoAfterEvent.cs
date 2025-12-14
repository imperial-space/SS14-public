using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.SkeletonInvasion;

[Serializable, NetSerializable]
public sealed partial class SkullPartAttachmentDoAfterEvent : SimpleDoAfterEvent
{
    public SkullPartAttachmentDoAfterEvent()
    {
    }
}
