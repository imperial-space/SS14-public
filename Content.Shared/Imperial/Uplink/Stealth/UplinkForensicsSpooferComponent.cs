using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Подделыватель криминалистики (forensics_spoofer).</summary>
[RegisterComponent]
public sealed partial class UplinkForensicsSpooferComponent : Component
{
    [ViewVariables]
    public HashSet<string> Fingerprints = new();

    [ViewVariables]
    public HashSet<string> Fibers = new();

    /// <summary>true — режим сканирования, false — нанесения.</summary>
    [ViewVariables]
    public bool ScanMode = true;
}
