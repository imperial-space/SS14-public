using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.AssaultPod;

/// <summary>
/// Устройство наведения штурмового пода (Paradise, /obj/item/assault_pod):
/// выбирает маяк станции и разворачивает десантную капсулу.
/// </summary>
[RegisterComponent]
public sealed partial class AssaultPodTargeterComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Pod;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public string? TargetName;
}

/// <summary>Десантная капсула: одноразово летит к выбранному маяку и садится внутри станции.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AssaultPodComponent : Component
{
    /// <summary>Отсчёт перед стартом.</summary>
    [DataField]
    public TimeSpan Countdown = TimeSpan.FromSeconds(10);

    /// <summary>Время полёта.</summary>
    [DataField]
    public TimeSpan FlightTime = TimeSpan.FromSeconds(20);

    /// <summary>Урон мобам прямо под капсулой.</summary>
    [DataField]
    public float CrushDamage = 40f;

    /// <summary>Урон постройкам на клетке посадки.</summary>
    [DataField]
    public float StructureDamage = 300f;

    [DataField]
    public float KnockdownRange = 2.5f;

    [DataField]
    public TimeSpan Knockdown = TimeSpan.FromSeconds(4);

    [ViewVariables, AutoNetworkedField]
    public AssaultPodState State = AssaultPodState.Ready;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public string? TargetName;

    [ViewVariables]
    public TimeSpan NextStage;

    /// <summary>Место посадки, отмеченное меткой за 5 с до касания.</summary>
    [ViewVariables]
    public Robust.Shared.Map.EntityCoordinates? LandingSpot;
}

[Serializable, NetSerializable]
public enum AssaultPodState : byte
{
    /// <summary>Стоит на базе, можно садиться.</summary>
    Ready,
    /// <summary>Идёт отсчёт, двери заперты.</summary>
    Launching,
    /// <summary>В полёте.</summary>
    Flying,
    /// <summary>Приземлилась, больше не летает.</summary>
    Landed,
}

/// <summary>
/// Метка зоны посадки пода (как pod_landingzone у капсул снабжения TG): поворачивается и сжимается к моменту касания.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AssaultPodLandingMarkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public TimeSpan Arrival;
}
