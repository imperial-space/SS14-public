using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>toggle_broadcast(): трансляция аватаров на развлекательные экраны.</summary>
[Serializable, NetSerializable]
public sealed class QuantumConsoleBroadcastMessage : BoundUserInterfaceMessage;
