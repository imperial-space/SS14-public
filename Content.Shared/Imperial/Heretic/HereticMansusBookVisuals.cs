using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic;

[Serializable, NetSerializable]
public enum HereticMansusBookVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum HereticMansusBookVisualState : byte
{
    Closed,
    Opening,
    Open,
    Closing,
}
