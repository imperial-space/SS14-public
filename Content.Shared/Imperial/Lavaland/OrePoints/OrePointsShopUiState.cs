using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.OrePoints;

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
