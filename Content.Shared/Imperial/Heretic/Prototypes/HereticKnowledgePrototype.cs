using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Prototypes;

[Prototype]
public sealed partial class HereticKnowledgePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>Localization key for the node name.</summary>
    [DataField]
    public LocId Name;

    /// <summary>Localization key for the node description.</summary>
    [DataField]
    public LocId Description;

    /// <summary>Knowledge point cost to research this node.</summary>
    [DataField]
    public int Cost = 1;

    /// <summary>Which path this knowledge belongs to.</summary>
    [DataField]
    public HereticPath Path = HereticPath.General;

    /// <summary>Set the heretic's path when this node is first researched.</summary>
    [DataField]
    public bool SetsPath = false;

    /// <summary>IDs of other knowledge nodes that must be researched first.</summary>
    [DataField]
    public List<ProtoId<HereticKnowledgePrototype>> Prerequisites = new();

    /// <summary>
    /// Alternative prerequisite sets - satisfied if ALL nodes in ANY one of these sets are researched.
    /// Used for shared/neutral knowledge reachable from several paths (e.g. wiki's adjacency-unlock nodes).
    /// Evaluated together with <see cref="Prerequisites"/> (both must pass if both are set).
    /// </summary>
    [DataField]
    public List<List<ProtoId<HereticKnowledgePrototype>>> PrerequisitesAny = new();

    /// <summary>Action prototypes to grant the heretic on research.</summary>
    [DataField]
    public List<EntProtoId> GrantActions = new();

    /// <summary>RSI path + state for the node icon shown in the knowledge menu.</summary>
    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>Marks this node as a free "gift" (ДАР) shown separately in the knowledge shop.</summary>
    [DataField]
    public bool IsGift = false;

    /// <summary>Shop tier this node belongs to (0 = not a shop node; 1-5 = shop levels).</summary>
    [DataField]
    public int ShopLevel = 0;

    /// <summary>IDs of knowledge nodes that are mutually exclusive with this one. Purchasing either blocks the other.</summary>
    [DataField]
    public List<ProtoId<HereticKnowledgePrototype>> ConflictsWith = new();

    /// <summary>If false, researching this node does not advance the shop level or grant a gift. Used for blade-upgrade nodes.</summary>
    [DataField]
    public bool AdvancesShopLevel = true;
}
