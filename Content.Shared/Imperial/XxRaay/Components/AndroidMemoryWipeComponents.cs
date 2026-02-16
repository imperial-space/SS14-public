namespace Content.Shared.Imperial.XxRaay.Components;

/// <summary>
/// Состояние обнуления памяти андроида
/// </summary>
[RegisterComponent]
public sealed partial class AndroidMemoryWipeInProgressComponent : Component
{
    /// <summary>
    /// Андроид RK800, инициировавший обнуление памяти
    /// </summary>
    public EntityUid? Wiper;

    /// <summary>
    /// Цель обнуления памяти
    /// </summary>
    public EntityUid? Target;

    /// <summary>
    /// Идёт ли сейчас попытка вырваться из захвата
    /// </summary>
    public bool EscapeInProgress;
}

/// <summary>
/// Компонент результата обнуления памяти
/// </summary>
[RegisterComponent]
public sealed partial class AndroidMemoryWipeResultComponent : Component
{
    /// <summary>
    /// Подтвердил ли игрок, что ознакомился с последствиями обнуления
    /// </summary>
    public bool Acknowledged;
}

