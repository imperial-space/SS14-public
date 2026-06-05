using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.LavalandShuttle;

[Serializable, NetSerializable]
public sealed class LavalandShuttleConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool CanFlyToStation;
    public bool CanFlyToLavaland;
}
