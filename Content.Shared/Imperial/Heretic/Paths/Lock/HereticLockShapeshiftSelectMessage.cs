using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic.Paths.Lock;

[Serializable, NetSerializable]
public sealed class HereticLockShapeshiftSelectMessage : BoundUserInterfaceMessage
{
    public HereticLockShapeshiftCreature Creature { get; }
    public HereticLockShapeshiftSelectMessage(HereticLockShapeshiftCreature creature) => Creature = creature;
}
