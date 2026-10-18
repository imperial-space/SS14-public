using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Experiments;

/// <summary>
/// Технодиск BEPIS (design_disk/bepis): хранит случайный экспериментальный узел TG.
/// Загрузка в консоль исследований раскрывает и сразу изучает этот узел.
/// </summary>
[RegisterComponent]
public sealed partial class TgBepisDiskComponent : Component
{
    /// <summary>SSresearch.techweb_nodes_experimental — узлы, из которых выбирается диск.</summary>
    [DataField(required: true)]
    public List<string> Nodes = new();

    [ViewVariables]
    public string? Node;
}

/// <summary>Деструктивный анализатор (destructive_analyzer.dm): разбирает предмет и раскрывает узлы TG.</summary>
[RegisterComponent]
public sealed partial class TgDestructiveAnalyzerComponent : Component
{
    [DataField]
    public string Slot = "analyzer_item";

    [DataField]
    public TimeSpan ProcessTime = TimeSpan.FromSeconds(2.7);

    [ViewVariables]
    public TimeSpan? DoneAt;

    [ViewVariables]
    public EntityUid? User;
}

[Serializable, NetSerializable]
public enum TgDestructiveAnalyzerVisuals : byte
{
    State,
}

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
