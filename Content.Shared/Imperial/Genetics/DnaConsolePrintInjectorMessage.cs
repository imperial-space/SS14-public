using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

[Serializable, NetSerializable]
public sealed class DnaConsolePrintInjectorMessage(DnaMutationRef mutation, bool activator) : BoundUserInterfaceMessage
{
    public readonly DnaMutationRef Mutation = mutation;
    public readonly bool Activator = activator;
}
