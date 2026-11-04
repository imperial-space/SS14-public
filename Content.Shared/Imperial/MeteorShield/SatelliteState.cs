using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.MeteorShield;

[Serializable, NetSerializable]
public sealed class SatelliteState(int id, bool active, string mode)
{
    public readonly int Id = id;
    public readonly bool Active = active;
    public readonly string Mode = mode;
}
