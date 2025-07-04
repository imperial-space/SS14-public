namespace Content.Shared.Imperial.Medieval.AreaMarker;

[ByRefEvent]
public sealed class AreaMarkerAnnounceEvent
{
    public string Message { get; set; }

    public AreaMarkerAnnounceEvent(string message)
    {
        Message = message;
    }
}
