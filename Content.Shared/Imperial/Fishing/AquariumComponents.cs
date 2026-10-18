using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Режим аквариума (AQUARIUM_MODE_*).</summary>
[Serializable, NetSerializable]
public enum ImperialAquariumMode : byte
{
    /// <summary>Полный ручной контроль среды и температуры, без стазиса.</summary>
    Manual,

    /// <summary>Встроенный процессор подбирает среду под население, может включить стазис.</summary>
    Auto,

    /// <summary>Рыбы не умирают от среды и голода, но не растут и не размножаются.</summary>
    Safe,
}

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
}

/// <summary>Декор аквариума (/obj/item/aquarium_prop).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialAquariumPropComponent : Component
{
    /// <summary>Состояние в tanks.rsi (в переносном аквариуме — с суффиксом _fish_tank).</summary>
    [DataField]
    public string? State;

    /// <summary>Свой спрайт вместо состояния tanks.rsi (икра луна-рыбы).</summary>
    [DataField]
    public string? Sprite;

    [DataField]
    public ImperialAquariumLayer Layer = ImperialAquariumLayer.Bottom;

    /// <summary>TRAIT_UNIQUE_AQUARIUM_CONTENT: только один такой предмет в аквариуме.</summary>
    [DataField]
    public bool Unique = true;
}

/// <summary>AQUARIUM_LAYER_MODE_*.</summary>
public enum ImperialAquariumLayer : byte
{
    BehindGlass,
    Bottom,
    Auto,
    Top,
}

/// <summary>Банка корма для рыб (/obj/item/reagent_containers/cup/fish_feed).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishFeedComponent : Component;

/// <summary>Улучшение аквариума (/obj/item/aquarium_upgrade): превращает один вид аквариума в другой.</summary>
[RegisterComponent]
public sealed partial class ImperialAquariumUpgradeComponent : Component
{
    [DataField(required: true)]
    public string From = default!;

    [DataField(required: true)]
    public string To = default!;
}

[Serializable, NetSerializable]
public enum ImperialAquariumUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ImperialAquariumFishEntry
{
    public NetEntity Entity;
    public string? Proto;
    public string Name = string.Empty;
    public bool Alive;

    /// <summary>get_happiness_value(): 0..5 сердечек.</summary>
    public int Happiness;
}

[Serializable, NetSerializable]
public sealed class ImperialAquariumPropEntry
{
    public NetEntity Entity;
    public string? Proto;
    public string Name = string.Empty;
}

[Serializable, NetSerializable]
public sealed class ImperialAquariumBoundUserInterfaceState : BoundUserInterfaceState
{
    public ImperialFishFluid Fluid;
    public float Temperature;
    public float MinTemperature;
    public float MaxTemperature;
    public ImperialAquariumMode Mode;
    public float FeedingIntervalMinutes;
    public List<ImperialAquariumFishEntry> Fish = new();
    public List<ImperialAquariumPropEntry> Props = new();
}

[Serializable, NetSerializable]
public sealed class ImperialAquariumSetTemperatureMessage(float temperature) : BoundUserInterfaceMessage
{
    public readonly float Temperature = temperature;
}

[Serializable, NetSerializable]
public sealed class ImperialAquariumSetFluidMessage(ImperialFishFluid fluid) : BoundUserInterfaceMessage
{
    public readonly ImperialFishFluid Fluid = fluid;
}

[Serializable, NetSerializable]
public sealed class ImperialAquariumSetModeMessage(ImperialAquariumMode mode) : BoundUserInterfaceMessage
{
    public readonly ImperialAquariumMode Mode = mode;
}

[Serializable, NetSerializable]
public sealed class ImperialAquariumSetFeedingIntervalMessage(float minutes) : BoundUserInterfaceMessage
{
    public readonly float Minutes = minutes;
}

[Serializable, NetSerializable]
public sealed class ImperialAquariumPetFishMessage(NetEntity fish) : BoundUserInterfaceMessage
{
    public readonly NetEntity Fish = fish;
}

[Serializable, NetSerializable]
public sealed class ImperialAquariumRemoveItemMessage(NetEntity item) : BoundUserInterfaceMessage
{
    public readonly NetEntity Item = item;
}

[Serializable, NetSerializable]
public sealed class ImperialAquariumRenameFishMessage(NetEntity fish, string name) : BoundUserInterfaceMessage
{
    public readonly NetEntity Fish = fish;
    public readonly string Name = name;
}

[Serializable, NetSerializable]
public enum ImperialAquariumVisuals : byte
{
    PanelOpen,
    Air,
    Broken,
}

/// <summary>Стартовое наполнение аквариума: один из вариантов рыбы со своим кормом (аквариум юриста).</summary>
[RegisterComponent]
public sealed partial class ImperialAquariumStartFillComponent : Component
{
    [DataField(required: true)]
    public List<ImperialAquariumStartFillEntry> Variants = new();
}

[DataDefinition]
public sealed partial class ImperialAquariumStartFillEntry
{
    [DataField]
    public float Weight = 1;

    [DataField]
    public List<Robust.Shared.Prototypes.EntProtoId> Contents = new();

    [DataField]
    public string? Reagent;

    [DataField]
    public float Amount;
}
