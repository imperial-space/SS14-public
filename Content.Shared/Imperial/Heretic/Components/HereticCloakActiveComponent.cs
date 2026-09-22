using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticCloakActiveComponent : Component
{
    public float OrigWalkSpeed;
    public float OrigSprintSpeed;
    public float OrigAcceleration;
    public string OriginalName = string.Empty;
    public EntityUid EffectEntity;
    public bool AddedStealth;
    public EntityUid? DisableActionEntity;
    public bool IsActive = true;
}
