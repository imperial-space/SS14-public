using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>mob/living/basic/spiderling/terror_spiderling.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderlingComponent : Component
{
    [DataField]
    public EntProtoId? GrowAs;

    [DataField]
    public float AmountGrown;

    [DataField]
    public bool Stillborn;

    [ViewVariables]
    public EntityUid? Queen;

    [ViewVariables]
    public EntityUid? Mother;

    [ViewVariables]
    public HashSet<EntityUid> Enemies = new();
}
