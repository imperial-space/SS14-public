namespace Content.Server.Imperial.SyndieBattle.Components;

[RegisterComponent]
public sealed partial class SyndieBattlePowerComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan RechargeInterval = TimeSpan.FromSeconds(2);

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextRecharge;

    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<EntityUid> Stations = new();
}
