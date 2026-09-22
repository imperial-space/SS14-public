using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticProphetGreavesComponent : Component
{
    [DataField]
    public EntProtoId GravityToggleAction = "ActionHereticGravityToggle";

    [DataField, AutoNetworkedField]
    public EntityUid? GravityToggleActionEntity;

    [DataField, AutoNetworkedField]
    public bool WeightlessEnabled;
}
