using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;

namespace Content.Server.Imperial.Quirks;

/// <summary>FOOD_FLAGS из TG: типы пищи, по которым работают аллергии и вкусы.</summary>
[Flags]
public enum ImperialFoodType
{
    None = 0,
    Meat = 1 << 0,
    Seafood = 1 << 1,
    Dairy = 1 << 2,
    Fruit = 1 << 3,
    Vegetables = 1 << 4,
    Grain = 1 << 5,
    Sugar = 1 << 6,
    Nuts = 1 << 7,
    Bugs = 1 << 8,
    Alcohol = 1 << 9,
    Junkfood = 1 << 10,
    Pineapple = 1 << 11,
    Raw = 1 << 12,
    Gross = 1 << 13,
}
