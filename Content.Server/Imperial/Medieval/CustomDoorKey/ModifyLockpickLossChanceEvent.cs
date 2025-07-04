namespace Content.Server.CustomDoorKey;

[ByRefEvent]
public record struct ModifyLockpickLossChanceEvent(float Modifier = 1f);
