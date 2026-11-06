using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

[Serializable, NetSerializable]
public enum CultConstructType : byte
{
    Juggernaut,
    Wraith,
    Artificer,
    Harvester,
    Proteon,
    Shade,
}
