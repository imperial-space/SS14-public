using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>FOOD_FLAGS TG (code/__DEFINES/food.dm).</summary>
[Flags]
public enum ImperialFoodTypes
{
    None = 0,
    Meat = 1 << 0,
    Vegetables = 1 << 1,
    Raw = 1 << 2,
    Junkfood = 1 << 3,
    Grain = 1 << 4,
    Fruit = 1 << 5,
    Dairy = 1 << 6,
    Fried = 1 << 7,
    Alcohol = 1 << 8,
    Sugar = 1 << 9,
    Gross = 1 << 10,
    Toxic = 1 << 11,
    Pineapple = 1 << 12,
    Breakfast = 1 << 13,
    Cloth = 1 << 14,
    Nuts = 1 << 15,
    Seafood = 1 << 16,
    Oranges = 1 << 17,
    Bugs = 1 << 18,
    Gore = 1 << 19,
    Stone = 1 << 20,
    Egg = 1 << 21,
}
