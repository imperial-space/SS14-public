namespace Content.Shared.Imperial.XxRaay.Components;

/// <summary>
/// Состояние принудительного снятия маскировки с андроида
/// </summary>
[RegisterComponent]
public sealed partial class AndroidForcedRevealComponent : Component
{
    /// <summary>
    /// Андроид, инициировавший снятие маскировки
    /// </summary>
    [ViewVariables]
    public EntityUid? Revealer;

    /// <summary>
    /// Цель снятия маскировки
    /// </summary>
    [ViewVariables]
    public EntityUid? Target;

    /// <summary>
    /// Идёт ли сейчас попытка вырваться из захвата
    /// </summary>
    [ViewVariables]
    public bool EscapeInProgress;
}

