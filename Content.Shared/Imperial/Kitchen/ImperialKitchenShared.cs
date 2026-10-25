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

/// <summary>crafting_complexity и foodtypes блюда (/obj/item/food TG). id — прототип сущности.</summary>
[Prototype]
public sealed partial class ImperialFoodDataPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public int Complexity;

    [DataField]
    public List<ImperialFoodTypes> Types = new();
}

/// <summary>quality реагента-напитка TG: 1 мерзко … 5 фантастика, 6 — изумительный вкус. id — реагент.</summary>
[Prototype]
public sealed partial class ImperialDrinkQualityPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public int Quality;
}

/// <summary>liked/disliked/toxic_foodtypes языка вида TG. id — вид SS14.</summary>
[Prototype]
public sealed partial class ImperialSpeciesTastePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ImperialFoodTypes> Liked = new();

    [DataField]
    public List<ImperialFoodTypes> Disliked = new();

    [DataField]
    public List<ImperialFoodTypes> Toxic = new();
}

public static class ImperialKitchen
{
    public const int QualityNormal = 1;
    public const int QualityTop = 7;
    public const int LikedChange = 2;
    public const int DislikedChange = -2;
    public const int ToxicThreshold = -8;

    public static ImperialFoodTypes Combine(IEnumerable<ImperialFoodTypes> types)
    {
        var result = ImperialFoodTypes.None;
        foreach (var type in types)
            result |= type;
        return result;
    }

    /// <summary>count_matching_foodtypes.</summary>
    public static int Count(ImperialFoodTypes a, ImperialFoodTypes b)
    {
        var bits = (int) (a & b);
        var count = 0;
        while (bits != 0)
        {
            count += bits & 1;
            bits >>= 1;
        }

        return count;
    }
}
