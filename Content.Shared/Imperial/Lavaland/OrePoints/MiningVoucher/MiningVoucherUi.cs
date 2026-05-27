using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.OrePoints.MiningVoucher;

[Serializable, NetSerializable]
public enum MiningVoucherUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class MiningVoucherSelectKitMessage : BoundUserInterfaceMessage
{
    public int KitIndex;

    public MiningVoucherSelectKitMessage(int kitIndex)
    {
        KitIndex = kitIndex;
    }
}
