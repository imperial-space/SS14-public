using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>Мать: королевское желе, перенос паучат, подогрев яиц.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderMotherComponent : Component
{
    [DataField]
    public float JellyCost = 100;
}
