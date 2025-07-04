namespace Content.Server.Imperial.Medieval.RandomSteal;

[ByRefEvent]
public record struct GetStealChanceModifiersEvent(float Modifier = 1f);
