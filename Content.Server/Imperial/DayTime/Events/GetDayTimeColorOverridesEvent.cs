namespace Content.Server.Imperial.DayTime;

[ByRefEvent]
public record struct GetDayTimeColorOverridesEvent(int Stage)
{
    public Color? OverrideColor = null;
}
