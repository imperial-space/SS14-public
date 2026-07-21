using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

[Serializable, NetSerializable]
public sealed partial class UniversalKeySetupDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}