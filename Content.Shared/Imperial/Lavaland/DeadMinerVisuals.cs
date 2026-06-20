using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland;

[Serializable, NetSerializable]
public enum DeadMinerVisuals : byte
{
    Transformed,
}

[Serializable, NetSerializable]
public enum DeadMinerVisualLayers : byte
{
    Base,
}
