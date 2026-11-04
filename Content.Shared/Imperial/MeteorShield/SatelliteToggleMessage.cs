using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.MeteorShield;

[Serializable, NetSerializable]
public sealed class SatelliteToggleMessage(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}
