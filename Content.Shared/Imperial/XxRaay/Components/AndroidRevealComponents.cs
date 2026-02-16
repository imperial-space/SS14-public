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
    public EntityUid? Revealer;

    /// <summary>
    /// Цель снятия маскировки
    /// </summary>
    public EntityUid? Target;

    /// <summary>
    /// Идёт ли сейчас попытка вырваться из захвата
    /// </summary>
    public bool EscapeInProgress;
}

