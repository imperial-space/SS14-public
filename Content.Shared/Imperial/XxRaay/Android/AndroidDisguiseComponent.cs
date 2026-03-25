using System;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.XxRaay.Android;

/// <summary>
/// Управляет состоянием маскировки андроида
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class AndroidDisguiseComponent : Component
{
    /// <summary>
    /// Состояние маскировки
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public AndroidDisguiseState State = AndroidDisguiseState.Android;

    /// <summary>
    /// Выбранное человеческое имя
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public string? HumanName;

    /// <summary>
    /// Исходное имя сущности
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public string? OriginalName;

    /// <summary>
    /// Длительность анимации перехода в человеческий вид
    /// </summary>
    [DataField]
    public TimeSpan TransformDuration = TimeSpan.FromSeconds(0.64);

    /// <summary>
    /// Длительность анимации обратного превращения в андроида
    /// </summary>
    [DataField]
    public TimeSpan RetransformDuration = TimeSpan.FromSeconds(0.64);

    /// <summary>
    /// Кд экшена
    /// </summary>
    [DataField]
    public TimeSpan ExtraCooldown = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Имя слоя корпуса андроида
    /// </summary>
    [DataField]
    public string AndroidBaseLayer = "android-base";

    /// <summary>
    /// Имя слоя анимации перехода в человеческий вид
    /// </summary>
    [DataField]
    public string AndroidTransformLayer = "android-transform";

    /// <summary>
    /// Имя слоя анимации обратного превращения в андроида
    /// </summary>
    [DataField]
    public string AndroidRetransformLayer = "android-retransform";

    [AutoPausedField]
    public TimeSpan NextStateTime;
}

public enum AndroidDisguiseState : byte
{
    Android,
    TransformingToHuman,
    Human,
    TransformingToAndroid
}

/// <summary>
/// Экшен переключения маскировки андроида
/// </summary>
public sealed partial class AndroidToggleDisguiseEvent : InstantActionEvent
{
}

/// <summary>
/// Ключ UI выбора человеческого имени при маскировке
/// </summary>
[Serializable, NetSerializable]
public enum AndroidDisguiseNameUiKey : byte
{
    Key
}

/// <summary>
/// Состояние UI выбора имени маскирующегося андроида
/// </summary>
[Serializable, NetSerializable]
public sealed class AndroidDisguiseNameBuiState : BoundUserInterfaceState
{
}

/// <summary>
/// Сообщение от клиента с выбранным человеческим именем для маскировки
/// </summary>
[Serializable, NetSerializable]
public sealed class AndroidDisguiseNameChosenMessage : BoundUserInterfaceMessage
{
    public readonly string Name;

    public AndroidDisguiseNameChosenMessage(string name)
    {
        Name = name;
    }
}

