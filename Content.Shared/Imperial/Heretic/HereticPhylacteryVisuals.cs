using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic;

[Serializable, NetSerializable]
public enum HereticPhylacteryVisuals : byte
{
    FillLevel,
}

[Serializable, NetSerializable]
public enum HereticPhylacteryFillLevel : byte
{
    Empty,
    Half,
    Full,
}
