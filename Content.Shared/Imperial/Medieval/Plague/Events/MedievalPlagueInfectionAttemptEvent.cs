using Content.Shared.Inventory;

namespace Content.Shared.Imperial.Medieval.Plague;

[ByRefEvent]
public record struct MedievalPlagueInfectionAttemptEvent() : IInventoryRelayEvent
{
    public float Probability = 1f;

    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;
}
