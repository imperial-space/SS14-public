using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>/obj/item/reagent_containers/cup/soup_pot: раствор и брошенные в него ингредиенты.</summary>
[RegisterComponent]
public sealed partial class ImperialSoupPotComponent : Component
{
    public const string IngredientsContainer = "soup_ingredients";

    [DataField]
    public string Solution = "pot";

    /// <summary>max_ingredients.</summary>
    [DataField]
    public int MaxIngredients = 24;
}
