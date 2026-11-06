using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>ProduceConsole: каталог, корзина (grocery_list), очки владельца карты и откат заказа.</summary>
[Serializable, NetSerializable]
public sealed class BitrunningVendorState(
    int points,
    List<string> categories,
    List<BitrunningVendorEntry> entries,
    Dictionary<string, int> cart,
    int totalCost,
    bool offCooldown,
    float cargoCostMultiplier,
    float expressCostMultiplier,
    int cargoValue) : BoundUserInterfaceState
{
    public int Points = points;
    public List<string> Categories = categories;
    public List<BitrunningVendorEntry> Entries = entries;
    public Dictionary<string, int> Cart = cart;
    public int TotalCost = totalCost;
    public bool OffCooldown = offCooldown;
    public float CargoCostMultiplier = cargoCostMultiplier;
    public float ExpressCostMultiplier = expressCostMultiplier;
    public int CargoValue = cargoValue;
}
