using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Kitchen;

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
