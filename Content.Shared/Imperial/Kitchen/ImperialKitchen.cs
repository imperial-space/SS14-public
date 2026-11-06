using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Kitchen;

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
        var bits = (int)(a & b);
        var count = 0;
        while (bits != 0)
        {
            count += bits & 1;
            bits >>= 1;
        }

        return count;
    }
}
