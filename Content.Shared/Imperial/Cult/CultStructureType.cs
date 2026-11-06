using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

[Serializable, NetSerializable]
public enum CultStructureType : byte
{
    None,
    Altar,
    Archives,
    Forge,
    Pylon,
}
