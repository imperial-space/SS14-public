using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.LavalandShuttle;

/// <summary>ui_data computer/shuttle/mining (tgui ShuttleConsole).</summary>
[Serializable, NetSerializable]
public sealed class LavalandShuttleConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public string TimerStr = "00:00";
    public LavalandShuttleStatus Status;
    public string Location = string.Empty;
    public List<LavalandShuttleDestination> Locations = new();
    public LavalandShuttleDestination Destination;
    public bool Locked;

    /// <summary>Оставшийся откат срочной отправки, секунды (0 — готова).</summary>
    public int ExpressCooldown;
}
