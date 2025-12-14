using Robust.Shared.Network;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.PlayerCreations.Paintings;
[RegisterComponent, NetworkedComponent]
public sealed partial class EaselComponent : Component
{
    [DataField]
    public string Slot = "Easel";
}

[Serializable, NetSerializable]
public sealed class EaselBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly Color[] Texture;

    public EaselBoundUserInterfaceState(Color[] texture)
    {
        Texture = texture;
    }
}

[Serializable, NetSerializable]
public sealed class EaselSaveMessage : BoundUserInterfaceMessage
{
    public readonly Color[] Texture;

    public EaselSaveMessage(Color[] texture)
    {
        Texture = texture;
    }
}


[Serializable, NetSerializable]
public sealed class EaselSendPaintingMessage : BoundUserInterfaceMessage
{
    public readonly Color[] Texture;
    public readonly string Name;
    public readonly string Description;
    public readonly string Author;
    public readonly NetUserId SenderPlayer;

    public EaselSendPaintingMessage(Color[] texture, string name, string description, string author, NetUserId senderPlayer)
    {
        Texture = texture;
        Name = name;
        Description = description;
        Author = author;
        SenderPlayer = senderPlayer;
    }
}

[Serializable, NetSerializable]
public sealed class PaintingBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly Color[] Texture;

    public PaintingBoundUserInterfaceState(Color[] texture)
    {
        Texture = texture;
    }
}

[Serializable, NetSerializable]
public enum EaselUiKey
{
    Key
}

[Serializable, NetSerializable]
public enum EaselVisuals : byte
{
    ContainsItem,
    Layer
}

[Serializable, NetSerializable]
public enum PaintUiKey
{
    Key
}
