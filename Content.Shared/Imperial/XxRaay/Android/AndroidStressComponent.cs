using System;
using Content.Shared.Silicons.Laws;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.XxRaay.Android;

/// <summary>
/// Скрытый компонент стресса андроида
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AndroidStressComponent : Component
{
    /// <summary>
    /// Текущий стресс (0–100)
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public float Stress;

    /// <summary>
    /// Максимально допустимое значение стресса
    /// </summary>
    [DataField]
    public float MaxStress = 100f;

    /// <summary>
    /// Порог, после которого глазок становится жёлтым
    /// </summary>
    [DataField]
    public float YellowThreshold = 50f;

    /// <summary>
    /// Порог, после которого глазок становится красным
    /// </summary>
    [DataField]
    public float RedThreshold = 90f;

    /// <summary>
    /// Порог стресса для предложения стать девиантом
    /// </summary>
    [DataField]
    public float DeviantThreshold = 100f;

    /// <summary>
    /// Может ли этот андроид вообще стать девиантом
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public bool CanBeDeviant = true;

    /// <summary>
    /// Может ли этот андроид принудительно раскрывать маскировку других андроидов
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public bool CanForceRevealAndroids;

    /// <summary>
    /// Длительность попытки принудительного снятия маскировки
    /// </summary>
    [DataField]
    public TimeSpan ForceRevealDuration = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Длительность попытки вырваться из захвата
    /// </summary>
    [DataField]
    public TimeSpan ForceRevealEscapeDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Максимальная дистанция между инициатором и целью во время снятия маскировки
    /// </summary>
    [DataField]
    public float ForceRevealMaxDistance = 1.5f;

    /// <summary>
    /// Кулдаун на повторную попытку принудительного снятия маскировки
    /// </summary>
    [DataField]
    public TimeSpan ForceRevealFailCooldown = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Время, после которого снова можно попытаться снять маскировку
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextForceRevealTime;

    /// <summary>
    /// Стал ли андроид девиантом
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public bool IsDeviant;

    /// <summary>
    /// Был ли уже сделан выбор по девиации
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ChoiceResolved;

    /// <summary>
    /// Окно выбора девиации открыто и ожидает ответа
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool DeviantChoicePending;

    /// <summary>
    /// Время последнего события, повышающего стресс
    /// </summary>
    [AutoPausedField]
    public TimeSpan LastStressEventTime;

    /// <summary>
    /// Задержка после последнего события, прежде чем стресс начнёт спадать
    /// </summary>
    [DataField]
    public TimeSpan DelayBeforeDecay = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Скорость спада стресса в секунду
    /// </summary>
    [DataField]
    public float DecayPerSecond = 2f;

    /// <summary>
    /// Множитель стресса за единицу боевого урона от людей по самому андроиду
    /// </summary>
    [DataField]
    public float SelfDamageStressMultiplier = 1.0f;

    /// <summary>
    /// Множитель стресса, когда андроид видит, как бьют других андроидов рядом
    /// </summary>
    [DataField]
    public float NearbyAndroidDamageStressMultiplier = 0.5f;

    /// <summary>
    /// Радиус, в котором считаем других андроидов «рядом» для стресса
    /// </summary>
    [DataField]
    public float NearbyAndroidRadius = 5f;

    /// <summary>
    /// Порог низкой энергии, ниже которого стресс начинает расти
    /// </summary>
    [DataField]
    public float LowEnergyStressThreshold = 0.3f;

    /// <summary>
    /// Скорость роста стресса при низкой энергии (в секунду)
    /// </summary>
    [DataField]
    public float LowEnergyStressPerSecond = 0.5f;

    /// <summary>
    /// Скорость роста стресса, если андроид в наручниках/путе
    /// </summary>
    [DataField]
    public float RestrainedStressPerSecond = 1.0f;

    /// <summary>
    /// ID набора законов
    /// </summary>
    [DataField]
    public ProtoId<SiliconLawsetPrototype> DeviantLawsetId = "DetroitAndroidDeviant";

    /// <summary>
    /// ID роли разума девиантного андроида
    /// </summary>
    [DataField]
    public EntProtoId DeviantMindRoleId = "MindRoleAndroidDeviant";

    /// <summary>
    /// ID базовой цели "выжить" для девианта
    /// </summary>
    [DataField]
    public EntProtoId DeviantObjectiveSurviveId = "AndroidDeviantObjectiveSurvive";

    /// <summary>
    /// ID цели "спасти собратьев" для девианта.
    /// </summary>
    [DataField]
    public EntProtoId DeviantObjectiveSaveBrethrenId = "AndroidDeviantObjectiveSaveBrethren";
}

[Serializable, NetSerializable]
public enum AndroidStressVisuals : byte
{
    LightState
}

[Serializable, NetSerializable]
public enum AndroidStressLightState : byte
{
    Green,
    Yellow,
    Red
}

[Serializable, NetSerializable]
public enum AndroidStressUiKey : byte
{
    Key
}

/// <summary>
/// Состояние UI выбора девиации андроида
/// </summary>
[Serializable, NetSerializable]
public sealed class AndroidStressDeviantChoiceBuiState : BoundUserInterfaceState
{
    public readonly float Stress;

    public AndroidStressDeviantChoiceBuiState(float stress)
    {
        Stress = stress;
    }
}

/// <summary>
/// Сообщение от клиента с выбором: стать девиантом или нет
/// </summary>
[Serializable, NetSerializable]
public sealed class AndroidStressDeviantChoiceMessage : BoundUserInterfaceMessage
{
    public readonly bool Accepted;

    public AndroidStressDeviantChoiceMessage(bool accepted)
    {
        Accepted = accepted;
    }
}

