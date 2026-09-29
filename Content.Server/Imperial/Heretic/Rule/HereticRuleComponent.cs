using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Heretic.Rule;

[RegisterComponent, Access(typeof(HereticRuleSystem))]
public sealed partial class HereticRuleComponent : Component
{
    [DataField]
    public int MaxHeretics = 1;

    /// <summary>Minds of selected heretics, stored for round-end summary.</summary>
    public readonly List<EntityUid> HereticMinds = new();
}
