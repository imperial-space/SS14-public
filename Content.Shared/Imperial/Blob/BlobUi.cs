using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob;

[Serializable, NetSerializable]
public enum BlobOvermindUiKey : byte
{
    /// <summary>Радиальное меню смены штамма (open_reroll_menu).</summary>
    Reroll,

    /// <summary>Выбор узла для прыжка (jump_to_node).</summary>
    Nodes,
}

[Serializable, NetSerializable]
public sealed class BlobRerollState(List<string> choices) : BoundUserInterfaceState
{
    public readonly List<string> Choices = choices;
}

[Serializable, NetSerializable]
public sealed class BlobSelectStrainMessage(string strain) : BoundUserInterfaceMessage
{
    public readonly string Strain = strain;
}

[Serializable, NetSerializable]
public sealed class BlobNodesState(List<NetEntity> nodes, List<string> names) : BoundUserInterfaceState
{
    public readonly List<NetEntity> Nodes = nodes;
    public readonly List<string> Names = names;
}

[Serializable, NetSerializable]
public sealed class BlobJumpToNodeMessage(NetEntity node) : BoundUserInterfaceMessage
{
    public readonly NetEntity Node = node;
}
