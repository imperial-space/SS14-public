using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>Серый: невидим на своей паутине, массовая паутина ИИ.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderGrayComponent : Component
{
    [DataField]
    public int MasswebChance = 10;
}
