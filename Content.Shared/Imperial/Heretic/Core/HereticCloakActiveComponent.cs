using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Core;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticCloakActiveComponent : Component
{
    [ViewVariables]
    public float OrigWalkSpeed;
    [ViewVariables]
    public float OrigSprintSpeed;
    [ViewVariables]
    public float OrigAcceleration;
    [ViewVariables]
    public string OriginalName = string.Empty;
    [ViewVariables]
    public EntityUid EffectEntity;
    [ViewVariables]
    public bool AddedStealth;
    [ViewVariables]
    public EntityUid? DisableActionEntity;
    [ViewVariables]
    public bool IsActive = true;
}
