using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic;

[Serializable, NetSerializable]
public enum HereticLockShapeshiftUiKey : byte { Key }

[Serializable, NetSerializable]
public enum HereticLockShapeshiftCreature : byte
{
    RustWalker = 0,
    AshOrb = 1,
    FleshWorm = 2,
    RawProphet = 3,
}

[Serializable, NetSerializable]
public sealed class HereticLockShapeshiftSelectMessage : BoundUserInterfaceMessage
{
    public HereticLockShapeshiftCreature Creature { get; }
    public HereticLockShapeshiftSelectMessage(HereticLockShapeshiftCreature creature) => Creature = creature;
}
