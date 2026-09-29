using Content.Server.Imperial.Heretic.Rule;

namespace Content.Server.Imperial.Heretic.Objectives;

[RegisterComponent, Access(typeof(HereticSacrificeConditionSystem), typeof(HereticRuleSystem))]
public sealed partial class HereticSacrificeConditionComponent : Component
{
    [DataField]
    public int RequiredSacrifices = 5;
}
