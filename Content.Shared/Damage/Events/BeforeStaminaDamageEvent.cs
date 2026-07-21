using Content.Shared.Inventory;

namespace Content.Shared.Damage.Events;

/// <summary>
/// Raised before stamina damage is dealt to allow other systems to cancel or modify it.
/// </summary>
[ByRefEvent]
public record struct BeforeStaminaDamageEvent(float Value, bool Cancelled = false, EntityUid? Origin = null) : IInventoryRelayEvent // Добавлен истоник урона, нужен для системы парирования
{
    SlotFlags IInventoryRelayEvent.TargetSlots =>  ~SlotFlags.POCKET;
}
