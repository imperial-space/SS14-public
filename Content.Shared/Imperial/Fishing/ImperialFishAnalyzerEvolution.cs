using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialFishAnalyzerEvolution
{
    public string Name = string.Empty;
    public string? Proto;
    public int Probability;
    public string Conditions = string.Empty;
}
