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
