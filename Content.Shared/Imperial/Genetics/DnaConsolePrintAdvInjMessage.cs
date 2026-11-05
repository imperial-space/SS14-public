using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

[Serializable, NetSerializable]
public sealed class DnaConsolePrintAdvInjMessage(string name) : BoundUserInterfaceMessage
{
    public readonly string Name = name;
}
