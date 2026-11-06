using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.OrePoints;

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
