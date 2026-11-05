using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Можно ли вставить хромосому (CHROMOSOME_NEVER / NONE / USED).</summary>
[Serializable, NetSerializable]
public enum DnaChromosomeState : byte
{
    Never,
    None,
    Used,
}
