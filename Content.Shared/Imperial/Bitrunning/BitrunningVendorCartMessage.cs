using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>add_one / remove_one / cart_set: новое количество товара в корзине (0–20).</summary>
[Serializable, NetSerializable]
public sealed class BitrunningVendorCartMessage(string id, int amount) : BoundUserInterfaceMessage
{
    public string Id = id;
    public int Amount = amount;
}
