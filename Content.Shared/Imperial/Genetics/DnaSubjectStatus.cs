using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

[Serializable, NetSerializable]
public enum DnaSubjectStatus : byte
{
    Conscious,
    SoftCrit,
    Unconscious,
    HardCrit,
    Dead,
    Transforming,
}
