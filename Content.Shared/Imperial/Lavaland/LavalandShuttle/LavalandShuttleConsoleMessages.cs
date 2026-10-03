using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.LavalandShuttle;

[Serializable, NetSerializable]
public enum LavalandShuttleDestination : byte
{
    None,
    Station,
    Lavaland,
}

[Serializable, NetSerializable]
public sealed class LavalandShuttleFlyToStationMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class LavalandShuttleFlyToLavalandMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class LavalandShuttleDepartMessage : BoundUserInterfaceMessage { }
