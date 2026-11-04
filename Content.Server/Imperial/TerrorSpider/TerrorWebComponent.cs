using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>obj/structure/spider/terrorweb.</summary>
[RegisterComponent]
public sealed partial class TerrorWebComponent : Component
{
    [DataField]
    public TerrorWebAbility Ability = TerrorWebAbility.None;

    /// <summary>Паутина матери не пропускает паучат.</summary>
    [DataField]
    public bool BlocksSpiderlings;
}
