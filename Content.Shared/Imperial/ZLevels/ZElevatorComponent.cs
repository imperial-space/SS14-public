using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>
/// Грузовой лифт: платформа Size×Size клеток с центром в этой сущности. Ездит по шахте (открытое пространство
/// или пустота над и под платформой) и везёт всё незакреплённое, что на ней стоит. С платформы не падают.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZElevatorComponent : Component
{
    [DataField]
    public int Size = 3;

    /// <summary>Время на один этаж.</summary>
    [DataField]
    public TimeSpan TravelTime = TimeSpan.FromSeconds(4);

    /// <summary>Клик и Alt-клик по платформе, кнопки вызова. Выключено — лифтом управляет только его консоль.</summary>
    [DataField]
    public bool ManualControl = true;

    /// <summary>Без своих звуков и всплывающих надписей при отправке и прибытии (их даёт консоль).</summary>
    [DataField]
    public bool Quiet;

    /// <summary>Карта этажа, куда едет лифт (только сервер).</summary>
    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public TimeSpan NextMove;
}
