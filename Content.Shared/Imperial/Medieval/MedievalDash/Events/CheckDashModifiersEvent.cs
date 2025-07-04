namespace Content.Shared.Imperial.Dash;

[ByRefEvent]
public record struct CheckDashCooldownModifiersEvent(float Modifier = 1f);

[ByRefEvent]
public record struct CheckDashStaminaCostModifiersEvent(float Modifier = 1f);

[ByRefEvent]
public record struct CheckDashDistanceModifiersEvent(float Modifier = 1f);
