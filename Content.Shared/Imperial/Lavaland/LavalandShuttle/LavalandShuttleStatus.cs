using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.LavalandShuttle;

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
