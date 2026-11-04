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
