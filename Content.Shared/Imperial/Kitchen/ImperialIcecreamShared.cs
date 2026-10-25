using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>/obj/item/food/icecream: рожок, в который автомат накладывает шарики (ice_cream_holder).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialIcecreamConeComponent : Component
{
    /// <summary>ingredients: что автомат тратит на сам рожок, по 1 ед.</summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> Ingredients = new();

    /// <summary>DEFAULT_MAX_ICE_CREAM_SCOOPS.</summary>
    [DataField]
    public int MaxScoops = 3;

    [DataField, AutoNetworkedField]
    public int Scoops;

    /// <summary>Цвет верхнего шарика.</summary>
    [DataField, AutoNetworkedField]
    public Color? Scoop;
}

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

/// <summary>/datum/ice_cream_flavour.</summary>
[Prototype]
public sealed partial class ImperialIcecreamFlavourPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public Color Color = Color.FromHex("#f2eede");

    /// <summary>CONE_REAGENT_NEEDED каждого — 1 ед.</summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> Ingredients = new();

    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    [DataField]
    public FixedPoint2 Amount = 3;
}
