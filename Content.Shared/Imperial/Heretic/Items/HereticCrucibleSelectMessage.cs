using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic.Items;

[Serializable, NetSerializable]
public sealed class HereticCrucibleSelectMessage : BoundUserInterfaceMessage
{
    public HereticCruciblePotionType Potion { get; }
    public HereticCrucibleSelectMessage(HereticCruciblePotionType potion) => Potion = potion;
}
