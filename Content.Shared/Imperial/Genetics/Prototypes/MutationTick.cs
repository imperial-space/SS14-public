using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics.Prototypes;

/// <summary>
/// Эффект мутации с шансом раз в секунду (SPT_PROB в SS13). Синхронизатор снижает шанс вдвое, сила усиливает эффекты.
/// </summary>
[DataDefinition]
public sealed partial class MutationTick
{
    /// <summary>Шанс срабатывания в секунду, от 0 до 1.</summary>
    [DataField(required: true)]
    public float Chance;

    [DataField]
    public EntityEffect[] Effects = Array.Empty<EntityEffect>();

    /// <summary>Выронить всё из рук.</summary>
    [DataField]
    public bool DropHeld;

    /// <summary>Носитель выкрикивает одну из фраз.</summary>
    [DataField]
    public List<LocId> Say = new();

    [DataField]
    public LocId? Popup;

    /// <summary>Только в сознании.</summary>
    [DataField]
    public bool RequiresConscious = true;

    /// <summary>Шанс растёт по мере падения стабильности (огненный пот: +(100 − стабильность) / 19.5 %).</summary>
    [DataField]
    public bool ScalesWithInstability;
}
