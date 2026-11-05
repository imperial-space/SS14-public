using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Генетический облик (genetic makeup): имя, ДНК и строки ферментов.</summary>
[Serializable, NetSerializable]
public sealed class DnaMakeupState
{
    public string Name = string.Empty;
    public string BloodType = string.Empty;
    public string UniqueEnzymes = string.Empty;
    public string UniqueIdentity = string.Empty;
    public string UniqueFeatures = string.Empty;
}
