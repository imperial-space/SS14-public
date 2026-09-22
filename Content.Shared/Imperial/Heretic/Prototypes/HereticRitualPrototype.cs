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

[DataDefinition]
public sealed partial class HereticRitualIngredient
{
    /// <summary>Entity prototype ID that must be present near the rune. Ignored if <see cref="Tag"/> is set.</summary>
    [DataField]
    public EntProtoId EntityId;

    /// <summary>If set, matches any entity carrying this tag instead of a specific prototype.</summary>
    [DataField]
    public ProtoId<TagPrototype>? Tag;

    /// <summary>If true, matches any entity that has a MobStateComponent (i.e. any living creature).</summary>
    [DataField]
    public bool HasMobState;

    /// <summary>If true, matches any living entity that is currently on fire.</summary>
    [DataField]
    public bool IsBurning;

    [DataField]
    public int Amount = 1;
}
