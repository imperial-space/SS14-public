using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fugitives;

/// <summary>/obj/machinery/fugitive_capture: блюспейс-камера для пойманных беглецов.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFugitiveCaptureComponent : Component
{
    /// <summary>Вариант псайкеров: сам затягивает беглецов вплотную.</summary>
    [DataField]
    public bool SuckIn;

    [DataField]
    public TimeSpan CaptureTime = TimeSpan.FromSeconds(5);
}
