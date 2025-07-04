namespace Content.Shared.Imperial.Medieval.Stamina;

[ByRefEvent]
public record struct GetStaminaCritDurationModifiersEvent(float Modifier = 1f);
