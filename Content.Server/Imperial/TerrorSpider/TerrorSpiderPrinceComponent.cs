using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>Принц: таран princely_charge.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderPrinceComponent : Component
{
    [ViewVariables]
    public bool Charging;
}
