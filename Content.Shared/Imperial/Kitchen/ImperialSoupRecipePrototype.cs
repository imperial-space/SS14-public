using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>/datum/chemical_reaction/food/soup: реакция в нагретой кастрюле с ингредиентами-предметами.</summary>
[Prototype]
public sealed partial class ImperialSoupRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Reagents = new();

    [DataField]
    public Dictionary<EntProtoId, int> Ingredients = new();

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Results = new();

    /// <summary>resulting_food_path: блюдо-предмет вместо супа.</summary>
    [DataField]
    public EntProtoId? ResultFood;

    [DataField]
    public float RequiredTemperature = 450f;

    /// <summary>ingredient_reagent_multiplier.</summary>
    [DataField]
    public float IngredientMultiplier = 0.8f;

    /// <summary>percentage_of_nutriment_converted.</summary>
    [DataField]
    public float NutrimentConverted = 0.25f;
}
