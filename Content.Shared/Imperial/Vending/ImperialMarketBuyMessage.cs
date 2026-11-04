using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

[Serializable, NetSerializable]
public sealed class ImperialMarketBuyMessage(string id, ImperialMarketShipping shipping) : BoundUserInterfaceMessage
{
    public readonly string Id = id;
    public readonly ImperialMarketShipping Shipping = shipping;
}
