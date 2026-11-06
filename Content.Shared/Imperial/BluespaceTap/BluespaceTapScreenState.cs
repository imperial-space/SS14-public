using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.BluespaceTap;

[Serializable, NetSerializable]
public enum BluespaceTapScreenState : byte
{
    None,
    Screen,
    Dirty,
    Cascade,
}
