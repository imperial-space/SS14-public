using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics.Prototypes;

/// <summary>
/// Срыв при распаде ДНК (instability_meltdown из SS13). Чем ниже ушла стабильность, тем вероятнее смертельный.
/// </summary>
[Prototype]
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
