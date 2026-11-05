using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Еретек чинит разбитую пусковую шахту.</summary>
[Serializable, NetSerializable]
public sealed partial class CadiaBerthRepairDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
