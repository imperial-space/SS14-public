using Robust.Shared.Network;

namespace Content.Server.Imperial.SyndieBattle;

[RegisterComponent]
public sealed partial class SyndieBattleScoreComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public NetUserId UserId;

    [ViewVariables(VVAccess.ReadOnly)]
    public int Generation;
}
