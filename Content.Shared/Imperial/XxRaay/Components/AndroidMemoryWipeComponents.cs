namespace Content.Shared.Imperial.XxRaay.Components;
using Robust.Shared.ViewVariables;

/// <summary>
/// Состояние обнуления памяти андроида
/// </summary>
[RegisterComponent]
public sealed partial class AndroidMemoryWipeInProgressComponent : Component
{
    /// <summary>
    /// Андроид RK800, инициировавший обнуление памяти
    /// </summary>
    [ViewVariables]
    public EntityUid? Wiper;

    /// <summary>
    /// Цель обнуления памяти
    /// </summary>
    [ViewVariables]
    public EntityUid? Target;

    /// <summary>
    /// Идёт ли сейчас попытка вырваться из захвата
    /// </summary>
    [ViewVariables]
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
    [ViewVariables]
    public bool Acknowledged;
}

