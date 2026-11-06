using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.FixedPoint;

namespace Content.Shared.Imperial.Fishing;

/// <summary>
/// Аквариум (/datum/component/aquarium + /obj/structure/aquarium, /obj/item/fish_tank).
/// Рыбы и декор лежат в контейнере <see cref="ContainerId"/>, корм — в растворе <see cref="FeedSolution"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialAquariumComponent : Component
{
    public const string ContainerId = "imperial-aquarium";
    public const string FeedSolution = "feed";

    [DataField, AutoNetworkedField]
    public ImperialFishFluid Fluid = ImperialFishFluid.Freshwater;

    [DataField, AutoNetworkedField]
    public float Temperature = ImperialFishing.DefaultAquariumTemp;

    [DataField, AutoNetworkedField]
    public float MinTemperature = ImperialFishing.MinAquariumTemp;

    [DataField, AutoNetworkedField]
    public float MaxTemperature = ImperialFishing.MaxAquariumTemp;

    [DataField, AutoNetworkedField]
    public ImperialAquariumMode Mode = ImperialAquariumMode.Auto;

    /// <summary>TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH от режима (безопасный режим или автоматический стазис).</summary>
    [DataField, AutoNetworkedField]
    public bool StopGrowth;

    /// <summary>TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH от тесноты (переносной аквариум переполнен).</summary>
    [DataField, AutoNetworkedField]
    public bool Cramped;

    /// <summary>TRAIT_AQUARIUM_PANEL_OPEN.</summary>
    [DataField, AutoNetworkedField]
    public bool PanelOpen;

    [DataField, AutoNetworkedField]
    public bool Broken;

    /// <summary>Интервал автокормления из хранилища корма.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan FeedingInterval = TimeSpan.FromMinutes(3);

    [ViewVariables]
    public TimeSpan LastFeeding;

    /// <summary>Аквариум-биогенератор (TRAIT_BIOELECTRIC_GENERATOR).</summary>
    [DataField]
    public bool BioelectricGenerator;

    /// <summary>Базовое имя состояний спрайта: aquarium, bioelec, fish_tank, fish_tank_bluespace.</summary>
    [DataField]
    public string BaseState = "aquarium";

    /// <summary>Переносной аквариум (/obj/item/fish_tank): другие оверлеи и состояния декора с суффиксом _fish_tank.</summary>
    [DataField]
    public bool Portable;

    /// <summary>Область, где плавают рыбы (пиксели спрайта 32×32, отсчёт снизу слева).</summary>
    [DataField]
    public Vector2i ZoneMin = new(2, 10);

    [DataField]
    public Vector2i ZoneMax = new(31, 28);

    /// <summary>maximum_relative_size: рыбы крупнее не помещаются (переносной аквариум).</summary>
    [DataField]
    public float? MaxFishSize;

    /// <summary>max_total_size: при превышении суммы размеров рыбы не растут и не размножаются.</summary>
    [DataField]
    public float? MaxTotalSize;

    /// <summary>slowdown_coeff: замедление от веса рыб в переносном аквариуме.</summary>
    [DataField]
    public float SlowdownCoeff;

    /// <summary>Текущая суммарная масса рыб (для замедления).</summary>
    [DataField, AutoNetworkedField]
    public float SummedWeight;

    [ViewVariables]
    public float SummedSize;

    [DataField]
    public TimeSpan InteractUsingDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan StartAdmireDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public FixedPoint2 SmashQuantity = 30;

    [DataField]
    public int RepairDoAfterAmount = 2;

    [DataField]
    public float ElectrogenesisRange = 5f;
}
