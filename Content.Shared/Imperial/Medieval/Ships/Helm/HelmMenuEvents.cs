using System;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Helm;

[Serializable, NetSerializable]
public enum HelmMenuAction : byte
{
    RotateLeft,
    Center,
    RotateRight
}

[Serializable, NetSerializable]
public enum HelmUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class HelmBoundUserInterfaceState : BoundUserInterfaceState
{
    public float HelmRotation;

    public HelmBoundUserInterfaceState(float helmRotation)
    {
        HelmRotation = helmRotation;
    }
}

[Serializable, NetSerializable]
public sealed class HelmMenuActionMessage : BoundUserInterfaceMessage
{
    public HelmMenuAction Action;

    public HelmMenuActionMessage(HelmMenuAction action)
    {
        Action = action;
    }
}

[Serializable, NetSerializable]
public sealed partial class HelmActionDoAfterEvent : DoAfterEvent
{
    public HelmMenuAction Action;

    public HelmActionDoAfterEvent(HelmMenuAction action)
    {
        Action = action;
    }

    public override DoAfterEvent Clone() => new HelmActionDoAfterEvent(Action);
}
