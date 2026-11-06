namespace Content.Server.Imperial.Heretic.Objectives;

[RegisterComponent, Access(typeof(HereticMajorSacrificeConditionSystem))]
public sealed partial class HereticMajorSacrificeConditionComponent : Component
{
    [DataField]
    public int RequiredSacrifices = 1;
}
