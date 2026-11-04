using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic.Paths.Lock;

[Serializable, NetSerializable]
public enum HereticLockShapeshiftCreature : byte
{
    RustWalker = 0,
    AshOrb = 1,
    FleshWorm = 2,
    RawProphet = 3,
}
