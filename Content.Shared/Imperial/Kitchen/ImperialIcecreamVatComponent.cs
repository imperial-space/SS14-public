using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>/obj/machinery/icecream_vat.</summary>
[RegisterComponent]
public sealed partial class ImperialIcecreamVatComponent : Component
{
    [DataField]
    public string Solution = "vat";

    /// <summary>VAT_MODE_CONES: выдаёт рожки вместо шариков.</summary>
    [DataField]
    public bool ConeMode;

    [DataField]
    public ProtoId<ImperialIcecreamFlavourPrototype> Flavour = "Vanilla";

    [DataField]
    public List<EntProtoId> Cones = new();
}
