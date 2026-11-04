using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology.Components;

public static class SlimeConstants
{
    public const int MinPower = 0;
    public const int MediumPower = 5;
    public const int MaxPower = 10;
    public const float MaxNutrition = 200f;
    public const float StartingNutrition = 100f;
    public const float GrowNutrition = 150f;
    public const float HungerNutrition = 50f;
    public const float StarveNutrition = 10f;
    public const int EvolutionThreshold = 10;
    public const float EvolutionCost = 100f;
    public const int ExtractCrossingRequired = 10;
    public const int OvercrowdAmount = 2;
    public const float TransparencyAlpha = 180f / 255f;
    public const int ExtraShockCost = 3;
    public const int BaseShockPercentage = 10;
    public const int ShockPercentagePerLevel = 7;
}
