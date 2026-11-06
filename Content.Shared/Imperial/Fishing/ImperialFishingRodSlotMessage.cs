using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>slot_action: пустая рука — вынуть, предмет в руке — вставить или поменять.</summary>
[Serializable, NetSerializable]
public sealed class ImperialFishingRodSlotMessage(string slot) : BoundUserInterfaceMessage
{
    public readonly string Slot = slot;
}
