using Content.Shared.Actions;

namespace Content.Shared.Imperial.Dash;

public sealed partial class MedievalDashEvent : WorldTargetActionEvent;

[ByRefEvent]
public record struct CanDashEvent(bool Cancelled = false);

[ByRefEvent]
public record struct CheckDashCooldownModifiersEvent(float Modifier = 1f);

[ByRefEvent]
public record struct CheckDashStaminaCostModifiersEvent(float Modifier = 1f);

[ByRefEvent]
public record struct CheckDashDistanceModifiersEvent(float Modifier = 1f);

[ByRefEvent]
public record struct DashStartedEvent();

[ByRefEvent]
public record struct DashEndedEvent();
