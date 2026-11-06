using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>Кнопка вызова лифта: зовёт ближайший лифт стопки на свой этаж.</summary>
[RegisterComponent]
public sealed partial class ZElevatorButtonComponent : Component
{
    /// <summary>Насколько далеко от края платформы может стоять кнопка.</summary>
    [DataField]
    public float Range = 4f;
}
