namespace Content.Shared.Imperial.DeimonFly.DeathNote.Scheduling;

/// <summary>
/// Чистые правила расчёта времени, общие для планировщика Тетради смерти и его тестов.
/// </summary>
public static class DeathNoteScheduleMath
{
    public static TimeSpan GetEffectiveExecutionDelay(
        TimeSpan requestedDelay,
        TimeSpan executionAdvance)
    {
        var safeDelay = requestedDelay > TimeSpan.Zero
            ? requestedDelay
            : TimeSpan.Zero;
        if (executionAdvance <= TimeSpan.Zero)
            return safeDelay;

        return safeDelay > executionAdvance
            ? safeDelay - executionAdvance
            : TimeSpan.Zero;
    }

    public static TimeSpan GetPreludeDelay(
        TimeSpan effectiveExecutionDelay,
        TimeSpan preludeDuration)
    {
        if (preludeDuration <= TimeSpan.Zero)
            return effectiveExecutionDelay;

        return effectiveExecutionDelay > preludeDuration
            ? effectiveExecutionDelay - preludeDuration
            : TimeSpan.Zero;
    }
}
