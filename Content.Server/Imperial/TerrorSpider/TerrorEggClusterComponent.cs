using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>obj/structure/spider/eggcluster/terror_eggcluster.</summary>
[RegisterComponent]
public sealed partial class TerrorEggClusterComponent : Component
{
    [DataField]
    public EntProtoId? SpiderlingType;

    [DataField]
    public int SpiderlingNumber = 1;

    [DataField]
    public float AmountGrown;

    [ViewVariables]
    public EntityUid? Queen;

    [ViewVariables]
    public EntityUid? Mother;

    [ViewVariables]
    public HashSet<EntityUid> Enemies = new();
}
