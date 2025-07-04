namespace Content.Server.Imperial.Medieval.Body;

[ByRefEvent]
public record struct GetBloodRegenModifiersEvent(float Modifier = 1f);
