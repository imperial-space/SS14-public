using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>quality реагента-напитка TG: 1 мерзко … 5 фантастика, 6 — изумительный вкус. id — реагент.</summary>
[Prototype]
public sealed partial class ImperialDrinkQualityPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public int Quality;
}
