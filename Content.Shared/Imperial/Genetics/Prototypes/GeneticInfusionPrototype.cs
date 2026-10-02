using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics.Prototypes;

/// <summary>
/// Набор ДНК-инфузера (infuser_entry из SS13): каждая инфузия существом-источником даёт следующую стадию,
/// а набравший порог получает бонус набора.
/// </summary>
[Prototype("geneticInfusion")]
public sealed partial class GeneticInfusionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>Какие существа годятся в источник (мёртвыми).</summary>
    [DataField(required: true)]
    public List<EntProtoId> Sources = new();

    /// <summary>Что даёт каждая следующая инфузия.</summary>
    [DataField]
    public List<ComponentRegistry> Stages = new();

    /// <summary>Сколько инфузий нужно для бонуса набора.</summary>
    [DataField]
    public int Threshold = 3;

    [DataField]
    public ComponentRegistry Bonus = new();

    [DataField]
    public LocId? BonusText;
}
