using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Experiments;

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
