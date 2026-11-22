namespace Content.Server.Imperial.SyndieBattle;

[RegisterComponent]
public sealed partial class SyndieBattleRedemptionComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int BaseReward = 4;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public double StoredTelecrystalMultiplier = 0.25;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxReward = 100;
}
