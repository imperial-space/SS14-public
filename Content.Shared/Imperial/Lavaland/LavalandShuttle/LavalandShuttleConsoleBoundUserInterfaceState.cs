using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.LavalandShuttle;

[Serializable, NetSerializable]
public sealed class LavalandShuttleConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public LavalandShuttleDestination SelectedDestination;
    public bool StationAvailable;
    public bool LavalandAvailable;
    public bool CanDepart;
}
