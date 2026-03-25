using System;

namespace Content.Shared.Imperial.XxRaay.Components;

/// <summary>
/// Компонент для андроидов серии RK800
/// </summary>
[RegisterComponent]
public sealed partial class AndroidRk800Component : Component
{
    /// <summary>
    /// Длительность обнуления памяти
    /// </summary>
    [DataField]
    public TimeSpan MemoryWipeDuration = TimeSpan.FromMinutes(1.5);

    /// <summary>
    /// Длительность попытки вырваться из захвата при обнулении памяти
    /// </summary>
    [DataField]
    public TimeSpan MemoryWipeEscapeDuration = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Максимальная дистанция между RK800 и целью во время обнуления памяти
    /// </summary>
    [DataField]
    public float MemoryWipeMaxDistance = 1.5f;

    /// <summary>
    /// Кулдаун на повторную попытку обнуления памяти после побега цели
    /// </summary>
    [DataField]
    public TimeSpan MemoryWipeFailCooldown = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Время, после которого снова можно пытаться обнулить память
    /// </summary>
    [ViewVariables]
    public TimeSpan NextMemoryWipeTime;
}

