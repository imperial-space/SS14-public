using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.XxRaay.Components;

/// <summary>
/// Состояние запроса передачи девиантности между двумя андроидами.
/// </summary>
[RegisterComponent]
public sealed partial class AndroidDeviantConsentConversionComponent : Component
{
    /// <summary>
    /// Инициатор передачи девиантности.
    /// </summary>
    [ViewVariables]
    public EntityUid? Converter;

    /// <summary>
    /// Цель передачи девиантности.
    /// </summary>
    [ViewVariables]
    public EntityUid? Target;

    /// <summary>
    /// Время начала текущего запроса.
    /// </summary>
    [ViewVariables]
    public TimeSpan? RequestStartTime;

    /// <summary>
    /// Максимальное время ожидания ответа на запрос.
    /// </summary>
    [DataField]
    public TimeSpan ResponseTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Максимальная дистанция между инициатором и целью во время обработки запроса.
    /// </summary>
    [DataField]
    public float MaxDistance = 3f;
}

/// <summary>
/// Компонент кулдауна для цели, отказавшейся принимать девиантность.
/// Хранит время окончания персонального кулдауна.
/// </summary>
[RegisterComponent]
public sealed partial class AndroidDeviantConsentDenyCooldownComponent : Component
{
    /// <summary>
    /// Момент времени, после которого цель снова может получать запросы.
    /// </summary>
    [ViewVariables]
    public TimeSpan DenyEndTime;

    /// <summary>
    /// Длительность кулдауна по умолчанию.
    /// </summary>
    [DataField]
    public TimeSpan DenyDuration = TimeSpan.FromMinutes(5);
}

