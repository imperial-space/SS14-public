using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Chaplain.Components;

/// <summary>
/// Набор святой брони. Все прототипы с этим компонентом попадают в меню маяка.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HolyArmamentsKitComponent : Component
{
    /// <summary>Предмет, иконка которого показывается в меню.</summary>
    [DataField(required: true)]
    public EntProtoId Preview;
}
