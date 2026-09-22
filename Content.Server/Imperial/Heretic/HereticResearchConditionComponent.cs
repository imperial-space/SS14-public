namespace Content.Server.Imperial.Heretic;

[RegisterComponent, Access(typeof(HereticResearchConditionSystem), typeof(Content.Server.GameTicking.Rules.HereticRuleSystem))]
public sealed partial class HereticResearchConditionComponent : Component
{
    [DataField]
    public int RequiredKnowledge = 17;
}
