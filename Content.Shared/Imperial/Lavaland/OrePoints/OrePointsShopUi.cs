using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.OrePoints;

[Serializable, NetSerializable]
public enum OrePointsShopUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class OrePointsShopBuyMessage : BoundUserInterfaceMessage
{
    public string ItemId;

    public OrePointsShopBuyMessage(string itemId)
    {
        ItemId = itemId;
    }
}

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

[Serializable, NetSerializable]
public sealed class OrePointsShopUiState : BoundUserInterfaceState
{
    public List<OrePointsShopEntryState> Entries;
    public int PlayerBalance;

    public OrePointsShopUiState(List<OrePointsShopEntryState> entries, int playerBalance)
    {
        Entries = entries;
        PlayerBalance = playerBalance;
    }
}

/// <summary>
/// Данные магазина для одного игрока: баланс у каждого свой (его ID-карта),
/// поэтому шлётся сообщением конкретному игроку, а не общим состоянием окна.
/// </summary>
[Serializable, NetSerializable]
public sealed class OrePointsShopDataMessage : BoundUserInterfaceMessage
{
    public List<OrePointsShopEntryState> Entries;
    public int PlayerBalance;

    public OrePointsShopDataMessage(List<OrePointsShopEntryState> entries, int playerBalance)
    {
        Entries = entries;
        PlayerBalance = playerBalance;
    }
}
