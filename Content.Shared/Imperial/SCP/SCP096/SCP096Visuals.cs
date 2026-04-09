using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.SCP.SCP096;

[Serializable, NetSerializable]
public enum SCP096Visuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum SCP096VisualState : byte
{
    Calm,
    Screaming,
    Chasing,
    Dead,
}
