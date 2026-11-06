using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.OrePoints;

[Serializable, NetSerializable]
public sealed class OrePointsShopEntryState
{
    public string ItemId;
    public string Name;
    public int Cost;

    public OrePointsShopEntryState(string itemId, string name, int cost)
    {
        ItemId = itemId;
        Name = name;
        Cost = cost;
    }
}
