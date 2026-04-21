using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ODM;

[Serializable, NetSerializable]
public enum OdmHookSide : byte
{
    Left,
    Right,
}

public sealed partial class OdmLeftHookActionEvent : WorldTargetActionEvent;

public sealed partial class OdmRightHookActionEvent : WorldTargetActionEvent;

public sealed partial class OdmRetractActionEvent : InstantActionEvent;