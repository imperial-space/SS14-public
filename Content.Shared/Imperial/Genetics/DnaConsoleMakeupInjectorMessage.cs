using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

[Serializable, NetSerializable]
public sealed class DnaConsoleMakeupInjectorMessage(int index, DnaMakeupType type) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
    public readonly DnaMakeupType Type = type;
}
