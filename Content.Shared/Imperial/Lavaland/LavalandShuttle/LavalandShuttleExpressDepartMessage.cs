using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.LavalandShuttle;

/// <summary>Срочная отправка: почти без прогрева, но с долгим откатом.</summary>
[Serializable, NetSerializable]
public sealed class LavalandShuttleExpressDepartMessage : BoundUserInterfaceMessage;
