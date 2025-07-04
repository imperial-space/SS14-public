namespace Content.Shared.Imperial.Dash;

[ByRefEvent]
public record struct CanDashEvent(bool Cancelled = false);
