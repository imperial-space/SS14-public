using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticCosmosCloakComponent : Component
{
    [DataField]
    public EntProtoId ToggleAction = "ActionToggleCosmosCloakLevitation";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    [DataField, AutoNetworkedField]
    public bool IsLevitating;

    [DataField, AutoNetworkedField]
    public EntityUid? Wearer;
}
