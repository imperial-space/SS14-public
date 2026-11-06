using Content.Shared.Cargo.Prototypes;
using Content.Shared.VendingMachines;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

[Serializable, NetSerializable]
public sealed class ImperialVendingBuyMessage(InventoryType type, string id) : BoundUserInterfaceMessage
{
    public readonly InventoryType Type = type;
    public readonly string Id = id;
}
