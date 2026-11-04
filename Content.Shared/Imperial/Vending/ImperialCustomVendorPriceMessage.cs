using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

[Serializable, NetSerializable]
public sealed class ImperialCustomVendorPriceMessage(NetEntity item, int price) : BoundUserInterfaceMessage
{
    public readonly NetEntity Item = item;
    public readonly int Price = price;
}
