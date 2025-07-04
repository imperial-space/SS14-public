namespace Content.Shared.Imperial.Medieval.Inventory;

[ByRefEvent]
public record struct GetEquipDelayModifiersEvent(float Modifier = 1f);
