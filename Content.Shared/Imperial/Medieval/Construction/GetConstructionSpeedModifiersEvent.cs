namespace Content.Shared.Imperial.Medieval.Construction;

[ByRefEvent]
public record struct GetConstructionSpeedModifiersEvent(float Modifier = 1f);
