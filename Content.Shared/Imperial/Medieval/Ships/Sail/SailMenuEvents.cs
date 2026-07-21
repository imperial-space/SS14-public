using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Sail;

[Serializable, NetSerializable]
public enum SailMenuAction : byte
{
    RotateLeft,
    ToggleFold,
    RotateRight,
}

[Serializable, NetSerializable]
public enum SailUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SailMenuActionMessage : BoundUserInterfaceMessage
{
    public SailMenuAction Action;

    public SailMenuActionMessage(SailMenuAction action)
    {
        Action = action;
    }
}

[Serializable, NetSerializable]
public sealed partial class SailFoldDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class SailRotateDoAfterEvent : DoAfterEvent
{
    public bool RotateLeft;

    public SailRotateDoAfterEvent(bool rotateLeft)
    {
        RotateLeft = rotateLeft;
    }

    public override DoAfterEvent Clone() => new SailRotateDoAfterEvent(RotateLeft);
}
