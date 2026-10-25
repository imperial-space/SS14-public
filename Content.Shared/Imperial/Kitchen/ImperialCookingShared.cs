using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>/datum/component/bakeable: в духовке через Time секунд становится Result.</summary>
[RegisterComponent]
public sealed partial class ImperialBakeableComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Result;

    /// <summary>required_bake_time в секундах.</summary>
    [DataField]
    public float Time = 120f;

    /// <summary>positive_result: хорошая выпечка или горелое.</summary>
    [DataField]
    public bool Positive = true;

    public float Progress;
}

/// <summary>/datum/component/grillable: на гриле через Time секунд становится Result.</summary>
[RegisterComponent]
public sealed partial class ImperialGrillableComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Result;

    [DataField]
    public float Time = 120f;

    [DataField]
    public bool Positive = true;

    public float Progress;
}

/// <summary>TOOL_* TG, которыми обрабатывают еду.</summary>
public enum ImperialKitchenTool : byte
{
    Knife,
    Saw,
    RollingPin,
    Scalpel,
}

[DataDefinition]
public sealed partial class ImperialProcessEntry
{
    [DataField(required: true)]
    public ImperialKitchenTool Tool;

    [DataField(required: true)]
    public EntProtoId Result;

    [DataField]
    public int Amount = 3;

    /// <summary>time_to_process в секундах.</summary>
    [DataField]
    public float Time = 2f;

    [DataField]
    public bool TableRequired;
}

/// <summary>/datum/element/processable: инструментом превращается в несколько других предметов.</summary>
[RegisterComponent]
public sealed partial class ImperialProcessableComponent : Component
{
    [DataField]
    public List<ImperialProcessEntry> Entries = new();
}

[Serializable, NetSerializable]
public sealed partial class ImperialProcessDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Entry;

    private ImperialProcessDoAfterEvent()
    {
    }

    public ImperialProcessDoAfterEvent(int entry)
    {
        Entry = entry;
    }

    public override DoAfterEvent Clone() => this;
}

/// <summary>Инструмент кухни (TOOL_KNIFE, TOOL_ROLLINGPIN…).</summary>
[RegisterComponent]
public sealed partial class ImperialKitchenToolComponent : Component
{
    [DataField(required: true)]
    public List<ImperialKitchenTool> Tools = new();

    /// <summary>toolspeed: множитель времени обработки.</summary>
    [DataField]
    public float Speed = 1f;
}

[Serializable, NetSerializable]
public enum ImperialKitchenVisuals : byte
{
    Active,
    Open,
    StoveOn,
}

/// <summary>/obj/machinery/oven: противень внутри, закрытая и запитанная духовка печёт.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialOvenComponent : Component
{
    public const string TraySlot = "oven_tray";

    [DataField, AutoNetworkedField]
    public bool Open;

    [DataField]
    public EntProtoId? StartingTray = "ImperialOvenTray";

    [DataField]
    public SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/Imperial/Kitchen/oven_open.ogg");

    [DataField]
    public SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/Imperial/Kitchen/oven_close.ogg");

    public TimeSpan NextSmell;
}

/// <summary>/obj/item/plate/oven_tray.</summary>
[RegisterComponent]
public sealed partial class ImperialOvenTrayComponent : Component;

/// <summary>/obj/machinery/griddle (и /obj/machinery/grill): то, что лежит сверху, жарится.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialGriddleComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool On;

    [DataField]
    public int MaxItems = 8;

    /// <summary>Гриль на угле: работает без питания, пока есть топливо.</summary>
    [DataField]
    public bool NeedsPower = true;

    [DataField]
    public float Fuel;

    [DataField]
    public SoundSpecifier ToggleSound = new SoundPathSpecifier("/Audio/Machines/click.ogg");
}

/// <summary>/datum/component/stove: греет стоящую на ней кастрюлю.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialStoveComponent : Component
{
    public const string PotSlot = "stove_pot";

    [DataField, AutoNetworkedField]
    public bool On;

    /// <summary>heat_coefficient.</summary>
    [DataField]
    public float HeatCoefficient = 0.033f;

    /// <summary>SOUP_BURN_TEMP + 80.</summary>
    [DataField]
    public float TargetTemperature = 620f;

    [DataField]
    public EntProtoId? StartingPot;
}

/// <summary>/obj/item/reagent_containers/cup/soup_pot: раствор и брошенные в него ингредиенты.</summary>
[RegisterComponent]
public sealed partial class ImperialSoupPotComponent : Component
{
    public const string IngredientsContainer = "soup_ingredients";

    [DataField]
    public string Solution = "pot";

    /// <summary>max_ingredients.</summary>
    [DataField]
    public int MaxIngredients = 24;
}

/// <summary>/datum/chemical_reaction/food/soup: реакция в нагретой кастрюле с ингредиентами-предметами.</summary>
[Prototype]
public sealed partial class ImperialSoupRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Reagents = new();

    [DataField]
    public Dictionary<EntProtoId, int> Ingredients = new();

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Results = new();

    /// <summary>resulting_food_path: блюдо-предмет вместо супа.</summary>
    [DataField]
    public EntProtoId? ResultFood;

    [DataField]
    public float RequiredTemperature = 450f;

    /// <summary>ingredient_reagent_multiplier.</summary>
    [DataField]
    public float IngredientMultiplier = 0.8f;

    /// <summary>percentage_of_nutriment_converted.</summary>
    [DataField]
    public float NutrimentConverted = 0.25f;
}

/// <summary>/obj/machinery/processor.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialFoodProcessorComponent : Component
{
    public const string Container = "processor";

    [DataField, AutoNetworkedField]
    public bool Processing;

    /// <summary>rating_speed деталей.</summary>
    [DataField]
    public float Speed = 1f;

    /// <summary>rating_amount деталей.</summary>
    [DataField]
    public int Amount = 1;

    public TimeSpan EndTime;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Machines/blender.ogg");
}

/// <summary>/datum/food_processor_process.</summary>
[Prototype]
public sealed partial class ImperialProcessorRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId Input;

    [DataField(required: true)]
    public EntProtoId Output;

    /// <summary>multiplier.</summary>
    [DataField]
    public int Multiplier = 1;

    /// <summary>time в секундах.</summary>
    [DataField]
    public float Time = 4f;
}
