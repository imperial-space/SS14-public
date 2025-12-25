using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;

[Serializable] [NetSerializable]
public enum ItemQuality : byte
{
    Worst,
    ReallyBad,
    Bad,
    Default,
    Good,
    Excellent
}
