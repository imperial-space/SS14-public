using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology.Components;

[Serializable, NetSerializable]
public enum SlimeLifeStage : byte
{
    Baby,
    Adult,
}

/// <summary>SLIME_MOOD_*: лицо слайма.</summary>
[Serializable, NetSerializable]
public enum SlimeMood : byte
{
    None,
    Angry,
    Mischievous,
    Pout,
    Sad,
    Smile,
    Cat,
}

/// <summary>SLIME_HUNGER_*.</summary>
[Serializable, NetSerializable]
public enum SlimeHunger : byte
{
    None,
    Hungry,
    Starving,
}

/// <summary>
/// mob/living/basic/slime: состояние слайма, нужное и серверу, и клиенту (спрайт, осмотр).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SlimeComponent : Component
{
    /// <summary>slime_type.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<SlimeTypePrototype> SlimeType = "SlimeGrey";

    /// <summary>Случайный тип при появлении (slime/random).</summary>
    [DataField]
    public bool RandomType;

    /// <summary>life_stage.</summary>
    [DataField, AutoNetworkedField]
    public SlimeLifeStage Stage = SlimeLifeStage.Baby;

    /// <summary>current_mood.</summary>
    [DataField, AutoNetworkedField]
    public SlimeMood Mood = SlimeMood.None;

    /// <summary>cores: сколько экстрактов осталось внутри.</summary>
    [DataField, AutoNetworkedField]
    public int Cores = 1;

    /// <summary>mutation_chance (0–100).</summary>
    [DataField, AutoNetworkedField]
    public float MutationChance = 30f;

    /// <summary>powerlevel (0–10).</summary>
    [DataField, AutoNetworkedField]
    public int PowerLevel;

    /// <summary>nutrition (0–200).</summary>
    [DataField, AutoNetworkedField]
    public float Nutrition = SlimeConstants.StartingNutrition;

    /// <summary>amount_grown (0–10).</summary>
    [DataField, AutoNetworkedField]
    public int AmountGrown;

    /// <summary>hunger_disabled: слайм не голодает (зелье послушания).</summary>
    [DataField, AutoNetworkedField]
    public bool HungerDisabled;

    /// <summary>mutator_used.</summary>
    [DataField, AutoNetworkedField]
    public bool MutatorUsed;

    /// <summary>crossbreed_modification.</summary>
    [DataField, AutoNetworkedField]
    public string? CrossbreedModification;

    /// <summary>applied_crossbreed_amount.</summary>
    [DataField, AutoNetworkedField]
    public int AppliedCrossbreedAmount;

    /// <summary>overcrowded.</summary>
    [DataField, AutoNetworkedField]
    public bool Overcrowded;

    /// <summary>BB_SLIME_RABID.</summary>
    [DataField, AutoNetworkedField]
    public bool Rabid;

    /// <summary>Слайм лишён ядер (спрайт «-cut»).</summary>
    [ViewVariables]
    public bool Cut => Cores <= 0;
}

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
