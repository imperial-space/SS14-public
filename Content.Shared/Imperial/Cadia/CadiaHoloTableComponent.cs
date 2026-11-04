using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Гололитический стол командования. Карта рисуется из настоящих этажей поля; нанесённое на неё видят все
/// столы той же стороны. Империум ставит на ней чертежи обороны — они появляются в мире голограммами.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaHoloTableComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaFaction Faction = CadiaFaction.Imperium;

    /// <summary>Доступен ли планировщик обороны (чертежи в мире).</summary>
    [DataField]
    public bool Planner = true;

    /// <summary>Только просмотр (гололитический визор командира): карту видно, править нельзя.</summary>
    [DataField, AutoNetworkedField]
    public bool ReadOnly;
}
