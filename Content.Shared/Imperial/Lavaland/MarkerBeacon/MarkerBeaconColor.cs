using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.MarkerBeacon;

[Serializable, NetSerializable]
public enum MarkerBeaconColor : byte
{
    Burgundy = 0,
    Bronze,
    Yellow,
    Lime,
    Olive,
    Jade,
    Teal,
    Cerulean,
    Indigo,
    Purple,
    Violet,
    Fuchsia
}

[Serializable, NetSerializable]
public enum MarkerBeaconVisuals : byte
{
    Color
}
