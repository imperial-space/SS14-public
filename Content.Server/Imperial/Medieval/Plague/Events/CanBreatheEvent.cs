namespace Content.Server.Imperial.Medieval.Plague;

[ByRefEvent]
public record struct CanBreatheEvent()
{
    public bool Cancelled = false;
}
