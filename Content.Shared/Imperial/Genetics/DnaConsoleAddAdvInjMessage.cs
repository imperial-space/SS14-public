using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

[Serializable, NetSerializable]
public sealed class DnaConsoleAddAdvInjMessage(DnaMutationRef mutation, string injector) : BoundUserInterfaceMessage
{
    public readonly DnaMutationRef Mutation = mutation;
    public readonly string Injector = injector;
}
