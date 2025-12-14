namespace Content.Server.Imperial.DayTime;

[ByRefEvent]
public record struct DayCycleStageChangedEvent(int PrevStage, int NextStage);
