using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Действие с позицией гена (pulse_gene): следующее основание, предыдущее или X.</summary>
[Serializable, NetSerializable]
public enum DnaGeneAction : byte
{
    Clear,
    Next,
    Prev,
}
