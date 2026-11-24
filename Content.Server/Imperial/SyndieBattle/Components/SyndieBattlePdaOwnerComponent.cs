using Robust.Shared.Network;

namespace Content.Server.Imperial.SyndieBattle.Components;

[RegisterComponent]
public sealed partial class SyndieBattlePdaOwnerComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public NetUserId UserId;

    [ViewVariables(VVAccess.ReadOnly)]
    public int Generation;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool RedemptionAllowed = true;
}
