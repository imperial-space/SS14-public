namespace Content.Shared.Imperial.Medieval.Medical;

[ByRefEvent]
public record struct GetHealingSpeedModifiersEvent(bool IsNotSelf, float Modifier = 1f);
