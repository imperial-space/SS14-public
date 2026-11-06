using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.LavalandShuttle;

[Serializable, NetSerializable]
public enum LavalandShuttleDestination : byte
{
    None,
    Station,
    Lavaland,
    /// <summary>Ледяная луна (Imperial/Icemoon).</summary>
    Icemoon,
}
