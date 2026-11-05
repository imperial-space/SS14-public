using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>purchase (рейсом карго) или express (сразу к покупателю).</summary>
[Serializable, NetSerializable]
public sealed class BitrunningVendorPurchaseMessage(bool express) : BoundUserInterfaceMessage
{
    public bool Express = express;
}
