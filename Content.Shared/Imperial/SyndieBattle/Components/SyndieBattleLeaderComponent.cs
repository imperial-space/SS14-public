using Content.Shared.Imperial.SyndieBattle.StatusIcons;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.SyndieBattle.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SyndieBattleLeaderComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<SyndieBattleLeaderIconPrototype> StatusIcon = "SyndieBattleLeader";
}
