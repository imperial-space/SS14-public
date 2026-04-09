// usings moved to file-level globals; removed unused using

namespace Content.Server.Imperial.SCP.SCP008.Components;

[RegisterComponent]
public sealed partial class SCP008InfectionAuraComponent : Component
{
    [DataField("radius")]
    public float Radius = 8f;

    [DataField("zombifyDelay")]
    public TimeSpan ZombifyDelay = TimeSpan.FromSeconds(12);

    [DataField("updateInterval")]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField("lookupFlags")]
    public LookupFlags LookupFlags = LookupFlags.Dynamic;

    [DataField("allowCritical")]
    public bool AllowCritical = false;

    [DataField("warningDelay")]
    public TimeSpan WarningDelay = TimeSpan.FromSeconds(3);

    [DataField("warningPopup")]
    public LocId WarningPopup = "scp008-warning-popup";

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public TimeSpan LastUpdate;

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> ExposureTime = new();
}
