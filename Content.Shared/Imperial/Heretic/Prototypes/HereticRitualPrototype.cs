using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Heretic.Prototypes;

[Prototype]
public sealed partial class HereticRitualPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public LocId Name;

    /// <summary>ID of the knowledge node that unlocks this ritual.</summary>
    [DataField]
    public ProtoId<HereticKnowledgePrototype> RequiredKnowledge;

    [DataField]
    public List<HereticRitualIngredient> Ingredients = new();

    /// <summary>Entity prototypes spawned when the ritual succeeds.</summary>
    [DataField]
    public List<EntProtoId> Results = new();
}
