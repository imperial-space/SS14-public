using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.BluespaceTap;

[Serializable, NetSerializable]
public enum BluespaceTapVisualState : byte
{
    Off,
    Level0,
    Level1,
    Level2,
    Level3,
    Level4,
    Level5,
    Cascade,
}
