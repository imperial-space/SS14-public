using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialFishingRodBoundUserInterfaceState : BoundUserInterfaceState
{
    public ImperialFishingRodSlotData Bait = new();
    public ImperialFishingRodSlotData Line = new();
    public ImperialFishingRodSlotData Hook = new();
    public string Description = string.Empty;
}
