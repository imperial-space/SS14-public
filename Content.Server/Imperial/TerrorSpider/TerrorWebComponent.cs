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

    [DataField]
    public TimeSpan WebCollideDuration = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan WebCollideDelay = TimeSpan.FromSeconds(7);

    [DataField]
    public TimeSpan WebSpecialAbilityGreenAmountDelay = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan WebSpecialAbilityGreenMaxDelay = TimeSpan.FromSeconds(120);

    [DataField]
    public float WebSpecialAbilityBlackAmount = 30f;
}
