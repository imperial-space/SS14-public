using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics.Prototypes;

[Serializable, NetSerializable]
public enum MutationQuality : byte
{
    Positive,
    MinorNegative,
    Negative,
}
