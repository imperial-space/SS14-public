using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions;

public sealed partial class StartupFactionDataEvent : EntityEventArgs
{
    public string Job;
    public string JobPrefix;
    public StartupFactionDataEvent(string job, string prefix)
    {
        Job = job;
        JobPrefix = prefix;
    }
}
