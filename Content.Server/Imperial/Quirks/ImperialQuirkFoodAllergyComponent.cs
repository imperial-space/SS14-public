using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;

namespace Content.Server.Imperial.Quirks;

/// <summary>/datum/quirk/item_quirk/food_allergic.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkFoodAllergyComponent : Component
{
    [ViewVariables]
    public ImperialFoodType Allergy;
}
