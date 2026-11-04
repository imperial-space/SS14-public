using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>AdjustEyeBlurry: временное размытие зрения.</summary>
[RegisterComponent]
public sealed partial class TerrorEyeBlurComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}
