using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

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

    [DataField]
    public TimeSpan SendNoMobsDuration = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan InteractUsingDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan InteractHandDelay = TimeSpan.FromSeconds(1.25);

    [DataField]
    public int BoxDisassembleAmount = 10;
    #endregion
}
