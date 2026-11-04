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
