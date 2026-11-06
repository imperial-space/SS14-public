using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>Паук, спрятанный в трубах при перемещении ИИ по вентиляции.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderVentTravelComponent : Component
{
    [ViewVariables]
    public EntityCoordinates Origin;
}
