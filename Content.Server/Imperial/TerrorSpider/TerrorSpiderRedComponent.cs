using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>Красный: enrage-ступени урона.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderRedComponent : Component
{
    [ViewVariables]
    public int Enrage;
}
