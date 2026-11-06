namespace Content.Server.Imperial.NullRod;

/// <summary>
/// Можно ли стереть сущность нуллродом.
/// </summary>
[ByRefEvent]
public record struct ImperialNullRodEraseAttemptEvent(bool Erasable = false);
