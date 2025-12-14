namespace Content.Shared.Imperial.Medieval.Body;

[ByRefEvent]
public record struct GetBloodRegenModifiersEvent(float Modifier = 1f);
