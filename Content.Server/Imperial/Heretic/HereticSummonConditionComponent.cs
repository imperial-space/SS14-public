namespace Content.Server.Imperial.Heretic;

[RegisterComponent, Access(typeof(HereticSummonConditionSystem))]
public sealed partial class HereticSummonConditionComponent : Component
{
    [DataField]
    public int RequiredSummons = 2;
}
