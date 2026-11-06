using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>Мать: королевское желе, перенос паучат, подогрев яиц.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderMotherComponent : Component
{
    [DataField]
    public float JellyCost = 100;

    [DataField]
    public TimeSpan RoyalJellyActionDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public float GatherSpiderlingsRange = 2.5f;

    [DataField]
    public float IncubateEggsRange = 0.5f;
}
