using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

/// <summary>Вид машины производственной линии (/obj/machinery/power/manufacturing/*).</summary>
public enum ImperialManufacturingKind : byte
{
    Unloader,
    Crusher,
    Smelter,
    Lathe,
    Crafter,
    Router,
    Sorter,
    StorageBox,
}

/// <summary>Фильтр сортировщика (/datum/sortrouter_filter/*).</summary>
[Serializable, NetSerializable]
public enum ImperialSorterFilterType : byte
{
    IsStack,
    IsOre,
    IsMail,
    NameContains,
    SpecificItem,
    SpecificKind,
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ImperialSorterFilter
{
    [DataField]
    public ImperialSorterFilterType Type;

    /// <summary>dir_target: куда толкать подходящий предмет.</summary>
    [DataField]
    public Direction Dir = Direction.North;

    /// <summary>value: текст или прототип.</summary>
    [DataField]
    public string Value = string.Empty;

    /// <summary>Имя прототипа для «конкретного предмета».</summary>
    [DataField]
    public string ValueName = string.Empty;

    [DataField]
    public bool Inverted;

    /// <summary>currently_listening: ждёт удара предметом по сортировщику.</summary>
    public bool Listening;

    public int Id;
}

/// <summary>Машина производственной линии. Содержимое — в контейнере <see cref="ContainerId"/>.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialManufacturingComponent : Component
{
    public const string ContainerId = "imperial-manufacturing";

    /// <summary>withheld: результаты, которые некуда было выдать.</summary>
    public const string OutContainerId = "imperial-manufacturing-out";

    /// <summary>MANUFACTURING_TURF_LAG_LIMIT.</summary>
    public const int TurfLagLimit = 10;

    [DataField(required: true)]
    public ImperialManufacturingKind Kind;

    /// <summary>allow_mob_bump_intake.</summary>
    [DataField]
    public bool AllowMobBumpIntake;

    /// <summary>Мощность на действие (power_cost / crush_cost / power_to_unload_crate), Вт.</summary>
    [DataField]
    public float PowerCost;

    [ViewVariables]
    public TimeSpan NextProcess;

    #region Разгрузчик
    [ViewVariables] public bool FlipSide;
    #endregion

    #region Дробилка
    [DataField] public int Capacity = 5;
    [ViewVariables] public Dictionary<string, float> HeldMats = new();
    #endregion

    #region Лат
    [ViewVariables] public string? DesignId;
    [ViewVariables] public Dictionary<string, int> Materials = new();
    [ViewVariables] public int MaxMaterials = 5000;
    [ViewVariables] public TimeSpan? BusyUntil;
    #endregion

    #region Сборщик
    /// <summary>Готовит еду вместо крафта (cooker).</summary>
    [DataField] public bool Cooking;
    [ViewVariables] public string? Recipe;
    [ViewVariables] public TimeSpan? CraftUntil;
    [ViewVariables] public List<EntityUid> WithheldList = new();
    #endregion

    #region Маршрутизатор
    [ViewVariables] public HashSet<Direction> DisabledDirs = new();
    [ViewVariables] public List<Direction> Directions = new();
    /// <summary>dir маршрутизатора: откуда пришёл последний предмет (стрелка «вход»).</summary>
    [ViewVariables] public Direction InDir = Direction.South;
    #endregion

    #region Сортировщик
    [DataField] public List<ImperialSorterFilter> Filters = new();
    [DataField] public Direction DirIfNotMet = Direction.South;
    [DataField] public int MaxFilters = 10;
    [ViewVariables] public TimeSpan? LaunchAt;
    [ViewVariables] public int NextFilterId;
    #endregion

    #region Ящик
    [DataField] public int MaxStuff = 16;
    #endregion
}

[Serializable, NetSerializable]
public enum ImperialManufacturingVisuals : byte
{
    /// <summary>Основное состояние спрайта.</summary>
    State,

    /// <summary>Стрелки входа/выхода: «dir:#цвет;…» (generate_io_overlays).</summary>
    Io,

    /// <summary>Стрелки маршрутизатора: «dir:in|out|bl;…».</summary>
    Router,

    /// <summary>Мигающее наложение (crafter_printing, lathe_printing, router_operate:dir); пусто — нет.</summary>
    Flick,
}

[Serializable, NetSerializable]
public enum ImperialManufacturingUiKey : byte
{
    Select,
    Sorter,
}

[Serializable, NetSerializable]
public sealed class ImperialManufacturingSelectOption
{
    public string Id = string.Empty;
    public string Name = string.Empty;
}

/// <summary>tgui_input_list: выбор чертежа, рецепта, фильтра.</summary>
[Serializable, NetSerializable]
public sealed class ImperialManufacturingSelectState(string title, string purpose, List<ImperialManufacturingSelectOption> options) : BoundUserInterfaceState
{
    public readonly string Title = title;
    public readonly string Purpose = purpose;
    public readonly List<ImperialManufacturingSelectOption> Options = options;
}

[Serializable, NetSerializable]
public sealed class ImperialManufacturingSelectMessage(string purpose, string id) : BoundUserInterfaceMessage
{
    public readonly string Purpose = purpose;
    public readonly string Id = id;
}

[Serializable, NetSerializable]
public sealed class ImperialSorterFilterData
{
    public int Id;
    public string Name = string.Empty;
    public bool Inverted;
    public Direction Dir;

    /// <summary>Текст фильтра name_contains; null — фильтр не текстовый.</summary>
    public string? TextValue;
}

[Serializable, NetSerializable]
public sealed class ImperialSorterState(List<ImperialSorterFilterData> filters, Direction unmet) : BoundUserInterfaceState
{
    public readonly List<ImperialSorterFilterData> Filters = filters;
    public readonly Direction UnmetDir = unmet;
}

[Serializable, NetSerializable]
public enum ImperialSorterAction : byte
{
    NewFilter,
    DelFilter,
    Rotate,
    RotateUnmet,
    Edit,
    ShiftUp,
    ShiftDown,
}

[Serializable, NetSerializable]
public sealed class ImperialSorterActionMessage(ImperialSorterAction action, int id = -1) : BoundUserInterfaceMessage
{
    public readonly ImperialSorterAction Action = action;
    public readonly int Id = id;
}

[Serializable, NetSerializable]
public sealed class ImperialSorterTextMessage(int id, string text) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
    public readonly string Text = text;
}

/// <summary>Разборка ящика отвёрткой (storagebox screwdriver_act).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialStorageBoxDisassembleDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Высыпать содержимое ящика рукой (storagebox attack_hand).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialStorageBoxDumpDoAfterEvent : SimpleDoAfterEvent;
