namespace Content.Shared.Imperial.Medieval.Climbing;

[ByRefEvent]
public record struct GetClimbDelayModifiersEvent(EntityUid User, float Modifier = 1f);
