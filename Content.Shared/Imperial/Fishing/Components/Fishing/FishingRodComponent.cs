using Content.Shared.Imperial.Fishing.Enums;
using Robust.Shared.Map;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Fishing.FishingRodComponents;

[RegisterComponent]
public sealed partial class FishingRodComponent : Component
{
    /// <summary>
    /// Слот улучшения удочки #1
    /// </summary>
    [DataField]
    public string UpgradesContainerIdOne = "FishingUpgradeSlot1";
    /// <summary>
    /// Слот улучшения удочки #2
    /// </summary>
    [DataField]
    public string UpgradesContainerIdTwo = "FishingUpgradeSlot2";
    [DataField]
    public EntProtoId StartAction = "ActionStartFishing";
    [DataField]
    public EntProtoId EndAction = "ActionEndFishing";
    [DataField]
    public EntProtoId Float = "FloatBase";
    /// <summary>
    /// EntityUid Поплавка
    /// </summary>
    [ViewVariables]
    public EntityUid? FloatEntity;
    /// <summary>
    /// true если в данный момент идёт рыбалка, false если бездействие
    /// </summary>
    [ViewVariables]
    public bool IsFishing = false;
    [ViewVariables]
    public EntityUid? StartActionEntity;
    [ViewVariables]
    public EntityUid? EndActionEntity;
    /// <summary>
    /// Отвечает за сообщение о том, что что-то попалось на крючок
    /// </summary>
    [ViewVariables]
    public bool PopupCaution = true;
    /// <summary>
    /// Минимальное время рыбалки
    /// </summary>
    [DataField]
    public float MinFishingTime = 15f;
    /// <summary>
    /// Максимальное время рыбалки
    /// </summary>
    [DataField]
    public float MaxFishingTime = 25f;
    /// <summary>
    /// Интервал между моментом выведения сообщения о том, что что-то на крючке и принудительным завершением рыбалки
    /// </summary>
    [DataField]
    public float IntervalVisualFishingTime = 5f;
    [ViewVariables]
    public float VisualFishingTime;
    [ViewVariables]
    public float FishingTime;
    [ViewVariables]
    public float AccumulatorVisual = 0f;
    /// <summary>
    /// Робастный рыбак
    /// </summary>
    [ViewVariables]
    public EntityUid User;
    /// <summary>
    /// Робастная удочка
    /// </summary>
    [ViewVariables]
    public EntityUid FishingRodUid;
    /// <summary>
    /// Коэффицент влияния катушки (Reel) на скорость рыбалки
    /// </summary>
    [DataField]
    public float FishingReelCoefficient = 1f;
    /// <summary>
    /// Коэффицент влияния катушки (Крючка) на скорость рыбалки
    /// </summary>
    [DataField]
    public float FishingHookCoefficient = 1f;
    /// <summary>
    /// Базовое значение коэффицентов FishingReelCoefficient и FishingHookCoefficient
    /// </summary>
    [DataField]
    public float BaseCoefficent = 1f;
    /// <summary>
    /// Максимальная дистанция для рыбалки
    /// </summary>
    [DataField]
    public float FishingDistance = 2f;
    /// <summary>
    /// Обязательные предметы при рыбалке в воде
    /// </summary>
    [DataField]
    public List<EntitySpawnEntry> WaterItems = new();
    /// <summary>
    /// Предметы при рыбалке в лаве и жидкой плазме
    /// </summary>
    [DataField]
    public List<EntitySpawnEntry> LavaItems = new();
    /// <summary>
    /// Предметы при рыбалке в бездне
    /// </summary>
    [DataField]
    public List<EntitySpawnEntry> VoidItems = new();
    /// <summary>
    /// Предметы доставаемые Дебаг-удочкой
    /// </summary>
    [DataField]
    public List<EntitySpawnEntry> DebugItems = new();
    /// <summary>
    /// Может ли удочка рыбачить в воде (по умолчанию true)
    /// </summary>
    [DataField]
    public bool FishingWater = true;
    /// <summary>
    /// Может ли удочка рыбачить в лаве и жидкой плазме (по умолчанию false)
    /// </summary>
    [DataField]
    public bool FishingLava = false;
    /// <summary>
    /// Может ли удочка рыбачить в бездне (по умолчанию false)
    /// </summary>
    [DataField]
    public bool FishingVoid = false;
    /// <summary>
    /// Это дебаг удочка (по умолчанию false)
    /// </summary>
    [DataField]
    public bool FishingDebug = false;
    [ViewVariables]
    public EntityCoordinates CoordsRod;
    [ViewVariables]
    public EntityCoordinates CoordsTarget;
    [ViewVariables]
    public bool IsDistanceNormal = true;
    /// <summary>
    /// Место рыбалки
    /// </summary>
    [ViewVariables]
    public PlaceID Place;

    /// <summary>
    /// Максимум улучшений в 1 удочке
    /// </summary>
    [DataField]
    public int MaxUpgradeCount = 2;
}

