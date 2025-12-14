using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Plague;

[Serializable, NetSerializable]
public enum BloodlettingVisuals
{
    Data,
    Layer
}

[Serializable, NetSerializable]
public enum BloodlettingResult : int
{
    None = 0,
    Infected = 1,
    Healthy = 2,
    Immune = 3,
    InfectedIncub = 4
}
