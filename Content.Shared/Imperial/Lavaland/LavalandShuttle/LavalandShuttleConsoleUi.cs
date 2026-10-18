using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.LavalandShuttle;

[Serializable, NetSerializable]
public enum LavalandShuttleDestination : byte
{
    None,
    Station,
    Lavaland,
}

/// <summary>status консоли шаттла SS13.</summary>
[Serializable, NetSerializable]
public enum LavalandShuttleStatus : byte
{
    Idle,
    Igniting,
    InTransit,
    Recharging,
    Locked,
    Missing,
}

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

[Serializable, NetSerializable]
public sealed class LavalandShuttleSetDestinationMessage(LavalandShuttleDestination destination) : BoundUserInterfaceMessage
{
    public LavalandShuttleDestination Destination = destination;
}

/// <summary>act('move'): обычная отправка с прогревом двигателей.</summary>
[Serializable, NetSerializable]
public sealed class LavalandShuttleDepartMessage : BoundUserInterfaceMessage;

/// <summary>Срочная отправка: почти без прогрева, но с долгим откатом.</summary>
[Serializable, NetSerializable]
public sealed class LavalandShuttleExpressDepartMessage : BoundUserInterfaceMessage;
