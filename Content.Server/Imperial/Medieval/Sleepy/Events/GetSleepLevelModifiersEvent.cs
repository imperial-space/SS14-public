namespace Content.Server.Imperial.Medieval.NeedSleep;

[ByRefEvent]
public record struct GetSleepLevelModifiersEvent(bool Sleeping, float Modifier = 1f);
