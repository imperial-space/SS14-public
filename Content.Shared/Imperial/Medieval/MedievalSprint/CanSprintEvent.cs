namespace Content.Shared.Imperial.Medieval.Sprint;

[ByRefEvent]
public record struct CanSprintEvent(bool Cancelled = false);
