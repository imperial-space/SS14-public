using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob;

[Serializable, NetSerializable]
public sealed class BlobJumpToNodeMessage(NetEntity node) : BoundUserInterfaceMessage
{
    public readonly NetEntity Node = node;
}
