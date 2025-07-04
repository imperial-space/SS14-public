namespace Content.Server.Imperial.Medieval.RandomSteal;

[ByRefEvent]
public record struct TryGetAdditionalStealTargetEvent(bool Success = false);
