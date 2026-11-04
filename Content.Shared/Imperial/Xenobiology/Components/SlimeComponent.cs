using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology.Components;

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
