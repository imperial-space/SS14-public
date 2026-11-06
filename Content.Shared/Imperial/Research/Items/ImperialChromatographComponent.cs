using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Хроматограф ВЭЖХ (chem_mass_spec): переносит выбранный реагент из левого стакана в правый.</summary>
[RegisterComponent]
public sealed partial class ImperialChromatographComponent : Component
{
    [DataField]
    public string InputSlot = "beakerIn";

    [DataField]
    public string OutputSlot = "beakerOut";
}
