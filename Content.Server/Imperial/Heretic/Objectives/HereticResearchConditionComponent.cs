using Content.Server.Imperial.Heretic.Rule;

namespace Content.Server.Imperial.Heretic.Objectives;

[RegisterComponent, Access(typeof(HereticResearchConditionSystem), typeof(HereticRuleSystem))]
public sealed partial class HereticResearchConditionComponent : Component
{
    [DataField]
    public int RequiredKnowledge = 17;
}
