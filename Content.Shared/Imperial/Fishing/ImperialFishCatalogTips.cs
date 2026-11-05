using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialFishCatalogTips
{
    public string Spots = string.Empty;
    public string FavoriteBait = string.Empty;
    public string DislikedBait = string.Empty;
    public List<string> Traits = new();
    public string Difficulty = string.Empty;
}
