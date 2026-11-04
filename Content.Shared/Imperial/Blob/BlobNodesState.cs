using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob;

[Serializable, NetSerializable]
public sealed class BlobNodesState(List<NetEntity> nodes, List<string> names) : BoundUserInterfaceState
{
    public readonly List<NetEntity> Nodes = nodes;
    public readonly List<string> Names = names;
}
