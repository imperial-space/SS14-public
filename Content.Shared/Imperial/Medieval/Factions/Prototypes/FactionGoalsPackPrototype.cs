using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Factions;

[Prototype]
public sealed class FactionGoalsPackPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<ProtoId<FactionGoalPrototype>> Goals = new();
}
