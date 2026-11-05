using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Импульс по символу уникальной идентичности (ui) или особенностей (uf).</summary>
[Serializable, NetSerializable]
public sealed class DnaConsoleMakeupPulseMessage(bool features, int index) : BoundUserInterfaceMessage
{
    public readonly bool Features = features;
    public readonly int Index = index;
}
