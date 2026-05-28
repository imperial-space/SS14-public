using System;
using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.XxRaay.Android;

/// <summary>
/// Компонент, который хранит настройки алерта энергии андроида
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class AndroidEnergyComponent : Component
{
    /// <summary>
    /// Прототип алерта, отображающего запас энергии
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> Alert = "AndroidEnergy";

    /// <summary>
    /// Интервал обновления алерта энергии
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Порог низкой энергии
    /// </summary>
    [DataField]
    public float LowEnergyThreshold = 0.3f;

    /// <summary>
    /// Порог критической энергии
    /// </summary>
    [DataField]
    public float CriticalEnergyThreshold = 0.1f;

    /// <summary>
    /// Множитель скорости при низкой энергии
    /// </summary>
    [DataField]
    public float LowSpeedModifier = 0.8f;

    /// <summary>
    /// Множитель скорости при критически низкой энергии
    /// </summary>
    [DataField]
    public float CriticalSpeedModifier = 0.5f;

    /// <summary>
    /// Время следующего обновления
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// Базовая скорость ходьбы
    /// </summary>
    [ViewVariables]
    public float? DefaultWalkSpeed;

    /// <summary>
    /// Базовая скорость бега
    /// </summary>
    [ViewVariables]
    public float? DefaultSprintSpeed;
}
