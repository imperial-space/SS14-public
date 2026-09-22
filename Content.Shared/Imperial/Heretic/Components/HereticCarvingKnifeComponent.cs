using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticCarvingKnifeComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Charges = 3;

    [DataField]
    public EntProtoId AlertRuneAction = "ActionHereticCarvingAlertRune";
    [DataField, AutoNetworkedField]
    public EntityUid? AlertRuneActionEntity;

    [DataField]
    public EntProtoId StunRuneAction = "ActionHereticCarvingStunRune";
    [DataField, AutoNetworkedField]
    public EntityUid? StunRuneActionEntity;

    [DataField]
    public EntProtoId MadnessRuneAction = "ActionHereticCarvingMadnessRune";
    [DataField, AutoNetworkedField]
    public EntityUid? MadnessRuneActionEntity;
}
