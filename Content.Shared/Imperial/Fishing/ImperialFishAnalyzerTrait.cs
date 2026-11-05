using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialFishAnalyzerTrait
{
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int Inheritability;
}
