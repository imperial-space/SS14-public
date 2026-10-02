using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics.Prototypes;

/// <summary>
/// Срыв при распаде ДНК (instability_meltdown из SS13). Чем ниже ушла стабильность, тем вероятнее смертельный.
/// </summary>
[Prototype("geneticMeltdown")]
public sealed partial class GeneticMeltdownPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public float Weight = 1f;

    [DataField]
    public bool Fatal;

    [DataField]
    public LocId? Popup;

    [DataField]
    public EntityEffect[] Effects = Array.Empty<EntityEffect>();

    /// <summary>Действие, которое нельзя описать штатными эффектами.</summary>
    [DataField]
    public MeltdownAction Action = MeltdownAction.None;
}

public enum MeltdownAction : byte
{
    None,

    /// <summary>Превращение в обезьяну через обезьяний ген.</summary>
    Monkey,

    /// <summary>Урон ×200 до конца жизни (damage_resistance −20000).</summary>
    Fragile,

    /// <summary>Медленный клеточный распад (decloning).</summary>
    Decloning,

    /// <summary>Отбрасывает в случайную сторону (go_away).</summary>
    Yeet,

    /// <summary>Ноги отказывают навсегда (paraplegic).</summary>
    Paraplegic,

    Gib,

    /// <summary>Тело рассыпается в прах.</summary>
    Dust,
}
