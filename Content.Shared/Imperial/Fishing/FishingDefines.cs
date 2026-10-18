namespace Content.Shared.Imperial.Fishing;

// Константы и флаги рыбалки TG (__DEFINES/fish.dm).

public static class ImperialFishing
{
    /// <summary>FISHING_DEFAULT_DIFFICULTY.</summary>
    public const int DefaultDifficulty = 15;

    /// <summary>FISHING_EASY_DIFFICULTY.</summary>
    public const int EasyDifficulty = 10;

    /// <summary>FISHING_MINIMUM_DIFFICULTY.</summary>
    public const int MinimumDifficulty = 6;

    public const int FavBaitDifficultyMod = -5;
    public const int DislikedBaitDifficultyMod = 15;
    public const int ExpertFisherDifficultyMod = -5;
    public const int TraitMinorDifficultyBoost = 5;

    /// <summary>FISHING_MINIGAME_AREA: высота полосы мини-игры в её единицах.</summary>
    public const float MinigameArea = 1000f;

    /// <summary>Пиксельная высота полосы мини-игры (MINIGAME_SLIDER_HEIGHT).</summary>
    public const int SliderHeight = 76;

    /// <summary>MINIGAME_BAIT_HEIGHT.</summary>
    public const int BaitPixelHeight = 27;

    public const int BaitTopAndBottomHeight = 6;
    public const int FishPixelHeight = 4;
    public const int CompletionBarHeight = 80;

    /// <summary>Тик мини-игры (SSfishing wait = 0.05 с).</summary>
    public const float MinigameTick = 0.05f;

    /// <summary>Пометка «пусто» в таблице улова (FISHING_DUD).</summary>
    public const string Dud = "dud";

    /// <summary>FISH_WEIGHT_MULT_WITHOUT_BAIT.</summary>
    public const float WeightMultWithoutBait = 0.15f;

    // Размеры рыбы (FISH_SIZE_*_MAX).
    public const int SizeTinyMax = 30;
    public const int SizeSmallMax = 50;
    public const int SizeNormalMax = 80;
    public const int SizeBulkyMax = 120;
    public const int SizeTwoHandsRequired = 135;
    public const int SizeHugeMax = 165;

    public const float MaxDeviationCoeff = 2.5f;
    public const float GrowthMult = 0.38f;
    public const float GrowthPeak = 0.45f;
    public const float SizeWeightGrowthMalus = 0.5f;
    public const float GrindResultsWeightDivisor = 500f;
    public const float FilletNumberSizeDivisor = 30f;
    public const float WeightSlowdown = 2100f;
    public const float WeightSlowdownDivisor = 500f;
    public const float WeightSlowdownExponent = 0.54f;
    public const float WeightForceDivisor = 250f;
    public const float WeightBiteDivisor = GrindResultsWeightDivisor * 0.4f;
    public const float NewFishBreedingTimeoutMult = 0.6f;
    public const float NewFishLastFeedingMult = 0.33f;
    public const float StarvingThreshold = 1f;

    public const float MinAquariumTemp = 273.15f;
    public const float MaxAquariumTemp = 373.15f;
    public const float DefaultAquariumTemp = 297.15f;

    public const int AquariumMaxBreedingPopulation = 20;

    // Опыт навыка рыбалки (SKILL_EXP_*).
    public static readonly float[] SkillExperience = { 0, 100, 250, 500, 900, 1500, 2500 };

    /// <summary>SKILL_VALUE_MODIFIER навыка рыбалки по уровням 1..7.</summary>
    public static readonly float[] SkillValueModifier = { 1, 0, -1, -3, -5, -7, -10 };

    /// <summary>FISHING_SKILL_EXP_PER_SECOND.</summary>
    public const float SkillExpPerSecond = 2500f / 900f;

    /// <summary>FISHING_SKILL_DIFFIULTY_EXP_MULT.</summary>
    public const float SkillDifficultyExpMult = 0.015f;

    public const int SkillLevelNone = 1;
    public const int SkillLevelNovice = 2;
    public const int SkillLevelApprentice = 3;
    public const int SkillLevelJourneyman = 4;
    public const int SkillLevelExpert = 5;
    public const int SkillLevelMaster = 6;
    public const int SkillLevelLegendary = 7;

    /// <summary>GET_FISH_WEIGHT_RANK.</summary>
    public static int GetWeightRank(float weight)
    {
        return Math.Max((int) MathF.Round(1 + MathF.Log2(MathF.Max(weight / WeightForceDivisor, 1))), 1);
    }

    /// <summary>GET_FISH_SLOWDOWN.</summary>
    public static float GetSlowdown(float weight)
    {
        return MathF.Round(MathF.Pow(weight / WeightSlowdownDivisor, WeightSlowdownExponent) - 1.3f, 1);
    }
}

/// <summary>fishing_line_traits.</summary>
[Flags]
public enum ImperialFishingLineTraits
{
    None = 0,
    Cloaked = 1 << 0,
    Bouncy = 1 << 1,
    Stiff = 1 << 2,
    Autoreel = 1 << 3,
    Phase = 1 << 4,
}

/// <summary>fishing_hook_traits.</summary>
[Flags]
public enum ImperialFishingHookTraits
{
    None = 0,
    Weighted = 1 << 0,
    Bidirectional = 1 << 1,
    NoEscape = 1 << 2,
    Ensnare = 1 << 3,
    Kill = 1 << 4,
}

/// <summary>FISHING_MINIGAME_RULE_*.</summary>
[Flags]
public enum ImperialFishingRules
{
    None = 0,
    Bidirectional = 1 << 0,
    NoEscape = 1 << 1,
    Kill = 1 << 2,
    NoExp = 1 << 3,
    Antigrav = 1 << 4,
    Flip = 1 << 5,
    Autoreel = 1 << 6,
    Camo = 1 << 7,
}

/// <summary>Качество наживки (TRAIT_*_QUALITY_BAIT).</summary>
public enum ImperialBaitQuality : byte
{
    None,
    Basic,
    Good,
    Great,
}

/// <summary>Типы пищи TG (foodtypes), по которым рыбы выбирают наживку.</summary>
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
}

/// <summary>Среда, нужная рыбе (AQUARIUM_FLUID_*).</summary>
public enum ImperialFishFluid : byte
{
    Freshwater,
    Saltwater,
    Sulphwater,
    Air,
    Anadromous,
    AnyWater,
}

/// <summary>Значки рыбы в мини-игре (FISH_ICON_*).</summary>
public enum ImperialFishIcon : byte
{
    Fish,
    Hostile,
    Star,
    Chunky,
    Slime,
    Coin,
    Gem,
    Crab,
    Jellyfish,
    Bottle,
    Bone,
    Electric,
    Weapon,
    Critter,
    Seed,
    Organ,
}

/// <summary>Вид движения рыбы в мини-игре (/datum/fish_movement/*).</summary>
public enum ImperialFishMovementType : byte
{
    Default,
    Slow,
    Zippy,
    Accelerando,
    Choppy,
    Plunger,
}
