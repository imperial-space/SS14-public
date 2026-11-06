using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research;

/// <summary>
/// Данные узла techweb TG, которых нет у TechnologyPrototype: каналы объявления (announce_channels).
/// </summary>
[Robust.Shared.Prototypes.Prototype]
public sealed partial class ImperialTgNodePrototype : Robust.Shared.Prototypes.IPrototype
{
    /// <summary>ID — как у технологии (ImperialTg...).</summary>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<Robust.Shared.Prototypes.ProtoId<Content.Shared.Radio.RadioChannelPrototype>> Channels = new();
}
