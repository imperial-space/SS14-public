namespace Content.Server._CP14.Workbench;

[ByRefEvent]
public record struct CheckWorkbenchCraftSpeedModifiersEvent(EntityUid Workbench, EntityUid User, float Modifier = 1f);
