using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>
/// Компонент, хранящий сохранённую точку телепортации (от TeleportPotion).
/// Удаляется после телепортации обратно.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class XenoTeleportSavedComponent : Component
{
    /// <summary>Сохранённые мировые координаты для возврата.</summary>
    [DataField]
    public MapCoordinates SavedCoordinates = MapCoordinates.Nullspace;
}
