namespace Content.Server.Imperial.Chaplain;

/// <summary>
/// Служит ли сущность тёмным силам — спрашивают обряды и святое оружие.
/// </summary>
[ByRefEvent]
public record struct ImperialUnholyCheckEvent(bool Unholy = false);
