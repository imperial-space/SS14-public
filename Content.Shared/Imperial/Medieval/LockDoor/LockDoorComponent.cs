using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Shared.Imperial.LockDoor.Components;

[RegisterComponent]
public sealed partial class LockDoorComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public List<string> AccessLists = new();
}

[Serializable, NetSerializable]
public sealed partial class LockDoorDoAfterEvent : SimpleDoAfterEvent { }
