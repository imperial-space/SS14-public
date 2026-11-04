using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.OrePoints;

[Serializable, NetSerializable]
public sealed class OrePointsShopBuyMessage : BoundUserInterfaceMessage
{
    public string ItemId;

    public OrePointsShopBuyMessage(string itemId)
    {
        ItemId = itemId;
    }
}
