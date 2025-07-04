using Content.Shared.Inventory;

namespace Content.Shared.BadSmell
{
    public sealed class CleaningActionEvent : EntityEventArgs, IInventoryRelayEvent
    {
        public SlotFlags TargetSlots { get; } = ~SlotFlags.INNERCLOTHING & ~SlotFlags.POCKET;
        public float CleaningAmount;

        public CleaningActionEvent(float cleaningAmount)
        {
            CleaningAmount = cleaningAmount;
        }
    }
}
