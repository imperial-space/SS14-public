namespace Content.Server.Imperial.Medieval.Boss;

[ByRefEvent]
public record struct BeforeBossDefeatEvent()
{
    public bool Cancelled = false;
}
