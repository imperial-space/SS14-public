namespace Content.Shared.Imperial.DeimonFly.DeathNote.Parsing;

/// <summary>
/// Проверки времени, которым сервер передаёт уже доверенное игровое время раунда.
/// </summary>
public static class DeathNoteInputValidator
{
    public static bool TryResolveScheduledTime(
        TimeSpan currentRoundTime,
        string? input,
        TimeSpan defaultDelay,
        out TimeSpan scheduledRoundTime,
        out DeathNoteFailureReason failureReason)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            scheduledRoundTime = currentRoundTime + defaultDelay;
            failureReason = DeathNoteFailureReason.None;
            return true;
        }

        if (!DeathNoteInputParser.TryParseRoundTime(input, out scheduledRoundTime))
        {
            failureReason = DeathNoteFailureReason.InvalidTime;
            return false;
        }

        if (scheduledRoundTime <= currentRoundTime)
        {
            failureReason = DeathNoteFailureReason.TimeInPast;
            return false;
        }

        failureReason = DeathNoteFailureReason.None;
        return true;
    }
}
