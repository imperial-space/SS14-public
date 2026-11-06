using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Варп-врата: переносят слугу Хаоса между кораблями флота еретиков.</summary>
[RegisterComponent]
public sealed partial class CadiaWarpGateComponent : Component
{
    /// <summary>Как подписать врата в списке у других врат; без названия берётся имя корабля.</summary>
    [DataField]
    public LocId? Label;

    [ViewVariables]
    public TimeSpan NextUse;
}
