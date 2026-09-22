namespace Content.Server.Imperial.Heretic;

[RegisterComponent, Access(typeof(HereticSacrificeConditionSystem), typeof(Content.Server.GameTicking.Rules.HereticRuleSystem))]
public sealed partial class HereticSacrificeConditionComponent : Component
{
    [DataField]
    public int RequiredSacrifices = 5;
}
