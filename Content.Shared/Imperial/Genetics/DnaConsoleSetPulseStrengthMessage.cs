using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

[Serializable, NetSerializable]
public sealed class DnaConsoleSetPulseStrengthMessage(int value) : BoundUserInterfaceMessage
{
    public readonly int Value = value;
}
