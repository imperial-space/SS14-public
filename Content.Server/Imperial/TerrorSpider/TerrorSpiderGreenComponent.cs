using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>Зелёный: кладка за каждые два обмотанных трупа.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderGreenComponent : Component
{
    [DataField]
    public int FeedingsToLay = 2;
}
