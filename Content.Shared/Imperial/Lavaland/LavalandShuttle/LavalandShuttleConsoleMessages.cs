using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.LavalandShuttle;

[Serializable, NetSerializable]
public sealed class LavalandShuttleFlyToStationMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class LavalandShuttleFlyToLavalandMessage : BoundUserInterfaceMessage { }
