using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

[Serializable, NetSerializable]
public sealed partial class UniversalLockpickHackDoAfterEvent : DoAfterEvent
{
    public int[] NewCode;
    public override DoAfterEvent Clone() => this;
}
