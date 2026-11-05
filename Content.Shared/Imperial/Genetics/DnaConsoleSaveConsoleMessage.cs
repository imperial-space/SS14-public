using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

[Serializable, NetSerializable]
public sealed class DnaConsoleSaveConsoleMessage(DnaMutationRef mutation) : BoundUserInterfaceMessage
{
    public readonly DnaMutationRef Mutation = mutation;
}
