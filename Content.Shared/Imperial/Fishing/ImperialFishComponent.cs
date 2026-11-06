using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Fishing;

/// <summary>
/// Рыба (/obj/item/fish): размер, вес, голод, здоровье, размножение, черты, предпочтения по наживке.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialFishComponent : Component
{
    #region Данные вида

    /// <summary>fish_id: для каталога и достижений.</summary>
    [DataField]
    public string? FishId;

    /// <summary>fish_id_redirect_path.</summary>
    [DataField]
    public EntProtoId? FishIdRedirect;

    /// <summary>FISH_FLAG_SHOW_IN_CATALOG.</summary>
    [DataField]
    public bool ShowInCatalog = true;

    /// <summary>FISH_DO_FLOP_ANIM.</summary>
    [DataField]
    public bool Flops = true;

    /// <summary>FISH_FLAG_EXPERIMENT_SCANNABLE.</summary>
    [DataField]
    public bool ExperimentScannable = true;

    [DataField]
    public int SpriteWidth = 4;

    [DataField]
    public int SpriteHeight = 3;

    /// <summary>dedicated_in_aquarium_icon_state.</summary>
    [DataField]
    public string? AquariumState;

    /// <summary>aquarium_vc_color.</summary>
    [DataField]
    public Color? AquariumColor;

    [DataField]
    public ImperialFishFluid RequiredFluid = ImperialFishFluid.Freshwater;

    [DataField]
    public float TemperatureMin = ImperialFishing.MinAquariumTemp;

    [DataField]
    public float TemperatureMax = ImperialFishing.MaxAquariumTemp;

    /// <summary>food: реагент, которым рыба питается.</summary>
    [DataField]
    public ProtoId<ReagentPrototype> Food = "Nutriment";

    [DataField]
    public TimeSpan FeedingFrequency = TimeSpan.FromMinutes(5);

    /// <summary>icon_state_dead.</summary>
    [DataField]
    public string? DeadState;

    /// <summary>death_text.</summary>
    /// <summary>inhand_icon_state: особый спрайт в руках (меч-рыба, щука, рыба-бензопила).</summary>
    [DataField]
    public string? InhandState;

    [DataField]
    public LocId DeathText = "imperial-fish-death";

    /// <summary>random_case_rarity.</summary>
    [DataField]
    public int RandomCaseRarity = 1000;

    /// <summary>fillet_type.</summary>
    [DataField]
    public EntProtoId? FilletType = "FoodMeatFish";

    [DataField]
    public int NumFillets = 1;

    [DataField]
    public int StablePopulation = 1;

    [DataField]
    public TimeSpan BreedingTimeout = TimeSpan.FromMinutes(2);

    [DataField]
    public List<EntProtoId> CompatibleTypes = new();

    /// <summary>spawn_types: что рождается вместо своего вида.</summary>
    [DataField]
    public List<EntProtoId>? SpawnTypes;

    [DataField]
    public List<string> EvolutionTypes = new();

    /// <summary>fish_traits (изначальные).</summary>
    [DataField]
    public List<ProtoId<ImperialFishTraitPrototype>> InitialTraits = new();

    [DataField]
    public ImperialFishMovementType Movement = ImperialFishMovementType.Default;

    /// <summary>fishing_difficulty_modifier.</summary>
    [DataField]
    public float DifficultyModifier;

    [DataField]
    public List<ImperialBaitIdentifier> FavoriteBait = new();

    [DataField]
    public List<ImperialBaitIdentifier> DislikedBait = new();

    [DataField]
    public float AverageSize = 50;

    [DataField]
    public float AverageWeight = 1000;

    [DataField]
    public float WeightSizeDeviation = 0.2f;

    /// <summary>Рыба дышит воздухом: допустимое давление (min_pressure / max_pressure).</summary>
    [DataField]
    public float MinPressure = 50f;

    [DataField]
    public float MaxPressure = 550f;

    [DataField]
    public int Beauty = 250;

    /// <summary>Значок в мини-игре для мастера рыбалки (specific_fish_icons).</summary>
    [DataField]
    public ImperialFishIcon Icon = ImperialFishIcon.Fish;

    /// <summary>Вкус сырой рыбы.</summary>
    [DataField]
    public LocId Taste = "imperial-fish-taste-raw";

    /// <summary>Наживка из этой рыбы (TRAIT_FISHING_BAIT + качество).</summary>
    [DataField]
    public ImperialBaitQuality BaitQuality = ImperialBaitQuality.None;

    /// <summary>Сила электрогенеза для биоэлектрического генератора (Дж).</summary>
    [DataField]
    public float ElectrogenesisPower = 2_000_000f;

    [DataField]
    public SoundSpecifier PetSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    #endregion

    #region Состояние

    [DataField, AutoNetworkedField]
    public bool Dead;

    /// <summary>Размер в см.</summary>
    [DataField, AutoNetworkedField]
    public float Size;

    /// <summary>Вес в «килокламах».</summary>
    [DataField, AutoNetworkedField]
    public float Weight;

    [DataField]
    public float MaximumSize;

    [DataField]
    public float MaximumWeight;

    [ViewVariables]
    public float TempSize;

    [ViewVariables]
    public float TempWeight;

    /// <summary>Черты этой особи.</summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<ImperialFishTraitPrototype>> Traits = new();

    /// <summary>max_integrity.</summary>
    [DataField]
    public float MaxIntegrity = 200f;

    /// <summary>integrity_failure: доля, при которой рыба умирает.</summary>
    [DataField]
    public float IntegrityFailure = 0.5f;

    [DataField]
    public float Integrity = 200f;

    [ViewVariables]
    public TimeSpan LastFeeding;

    [ViewVariables]
    public TimeSpan BreedingWait;

    [DataField, AutoNetworkedField]
    public int BitesAmount;

    [DataField, AutoNetworkedField]
    public string? CatcherName;

    [DataField, AutoNetworkedField]
    public string? CatchDate;

    [ViewVariables]
    public TimeSpan PettedUntil;

    [ViewVariables]
    public float TimeOnSafeSpot;

    /// <summary>Только что пойман (TRAIT_FISH_JUST_SPAWNED).</summary>
    [ViewVariables]
    public bool JustSpawned;

    /// <summary>TRAIT_FISH_LOW_PRICE (купленная рыба).</summary>
    [DataField]
    public bool LowPrice;

    /// <summary>Приготовлена (TRAIT_FISH_WELL_COOKED).</summary>
    [DataField]
    public bool WellCooked;

    /// <summary>Жарилась (TRAIT_FOOD_FRIED / BBQ).</summary>
    [DataField]
    public bool Cooked;

    /// <summary>Отредактирована генной пушкой.</summary>
    [DataField]
    public bool Genegunned;

    /// <summary>FISH_FLAG_OVERPOPULATED (выставляет аквариум).</summary>
    [ViewVariables]
    public bool Overpopulated;

    /// <summary>FISH_FLAG_SAFE_TEMPERATURE (выставляет аквариум).</summary>
    [ViewVariables]
    public bool SafeTemperature;

    /// <summary>FISH_FLAG_SAFE_FLUID (выставляет аквариум).</summary>
    [ViewVariables]
    public bool SafeFluid;

    [ViewVariables]
    public TimeSpan NextElectrogenesis;

    [ViewVariables]
    public TimeSpan? ReviveAt;

    /// <summary>TRAIT_FISH_FED_LUBE: кормили смазкой (эволюция в смазкорыбу).</summary>
    [ViewVariables]
    public TimeSpan FedLubeUntil;

    /// <summary>TRAIT_FISH_MUTAGENIC: кормили мутагеном (шанс эволюции ×3).</summary>
    [ViewVariables]
    public TimeSpan MutagenicUntil;

    /// <summary>TRAIT_FISH_QUICK_GROWTH: кормили сывороткой роста.</summary>
    [ViewVariables]
    public TimeSpan QuickGrowthUntil;

    /// <summary>TRAIT_FISH_ON_TESLIUM: кормили теслием (электрогенез, но вредно).</summary>
    [ViewVariables]
    public TimeSpan TesliumUntil;

    #endregion
}

