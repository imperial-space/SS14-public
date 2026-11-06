using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>/datum/orderable_item/bitrunning_*: товар торгомата NexaCache за очки битраннинга.</summary>
[Prototype]
public sealed partial class BitrunningOrderPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Строкой, а не EntProtoId: часть товаров из других веток (Лаваленд, боевые искусства), без них заказ скрыт.</summary>
    [DataField(required: true)]
    public string Item = string.Empty;

    [DataField(required: true)]
    public int Cost;

    /// <summary>flair, tech.</summary>
    [DataField]
    public string Category = "tech";

    [DataField]
    public LocId? Description;
}
