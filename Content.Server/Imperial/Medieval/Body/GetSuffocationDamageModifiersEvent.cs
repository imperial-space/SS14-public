namespace Content.Server.Imperial.Medieval.Body;

[ByRefEvent]
public record struct GetSuffocationDamageModifiersEvent(float Modifier = 1f);
