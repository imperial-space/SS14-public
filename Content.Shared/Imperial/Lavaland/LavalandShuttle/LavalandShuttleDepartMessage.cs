using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.LavalandShuttle;

/// <summary>act('move'): обычная отправка с прогревом двигателей.</summary>
[Serializable, NetSerializable]
public sealed class LavalandShuttleDepartMessage : BoundUserInterfaceMessage;
