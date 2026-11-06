using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Experiments;

/// <summary>required_items_to_unlock: какие предметы раскрывают скрытый узел в деструктивном анализаторе.</summary>
[Prototype("imperialTgRevealRule")]
public sealed partial class TgRevealRulePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Node = string.Empty;

    [DataField]
    public List<EntProtoId> Prototypes = new();

    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>Любой предмет с контрабандой этого уровня (syndicate_basic: всё из аплинка).</summary>
    [DataField]
    public string? ContrabandSeverity;
}
