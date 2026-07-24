using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;

/// <summary>
/// Один реагент из приоритетного списка для отравления настоящей еды или напитка.
/// Первый вариант, смертельная доза которого помещается в раствор, используется сценарием.
/// </summary>
[DataDefinition]
public sealed partial class DeathNoteConsumablePoisonOption
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField(required: true)]
    public FixedPoint2 MinimumQuantity;

    [DataField(required: true)]
    public FixedPoint2 MaximumQuantity;
}
