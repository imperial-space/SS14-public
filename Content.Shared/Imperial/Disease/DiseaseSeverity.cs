using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Disease;

/// <summary>DISEASE_SEVERITY_*.</summary>
[Serializable, NetSerializable]
public enum DiseaseSeverity : byte
{
    Uncurable,
    Positive,
    NonThreat,
    Minor,
    Medium,
    Harmful,
    Dangerous,
    Biohazard,
}
