using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.LavalandShuttle;

[Serializable, NetSerializable]
public sealed class LavalandShuttleSetDestinationMessage(LavalandShuttleDestination destination) : BoundUserInterfaceMessage
{
    public LavalandShuttleDestination Destination = destination;
}
