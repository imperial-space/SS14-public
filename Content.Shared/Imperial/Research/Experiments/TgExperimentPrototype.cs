using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Research.Experiments;

/// <summary>
/// Эксперимент techweb TG (code/modules/experisci): обязательный для узла (required_experiments)
/// или дающий скидку на узел (discount_experiments). Скидка в SS14 — возврат очков на сервер.
/// </summary>
[Prototype("imperialTgExperiment")]
public sealed partial class TgExperimentPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    /// <summary>Узел TG (например, selection), которому нужен или на который даёт скидку этот эксперимент.</summary>
    [DataField(required: true)]
    public string Node = string.Empty;

    /// <summary>required_experiments: без эксперимента узел не открыть.</summary>
    [DataField]
    public bool Required;

    /// <summary>discount_experiments: сколько очков исследований вернуть серверу.</summary>
    [DataField]
    public int Discount;

    [DataField(required: true)]
    public TgExperimentRequirement Requirement = default!;
}
