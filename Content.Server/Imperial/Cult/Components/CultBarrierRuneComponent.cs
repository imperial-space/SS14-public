using Robust.Shared.ViewVariables;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>
/// Компонент барьерной руны — хранит ссылку на спавненный барьер.
/// Добавляется к CultRune типа Barrier при первой активации.
/// </summary>
[RegisterComponent]
public sealed partial class CultBarrierRuneComponent : Component
{
    /// <summary>Барьерная сущность, созданная этой руной. Null если барьер не активен.</summary>
    [ViewVariables]
    public EntityUid? BarrierEntity { get; set; }
}
