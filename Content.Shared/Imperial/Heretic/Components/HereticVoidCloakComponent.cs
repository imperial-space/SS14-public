using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticVoidCloakComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsHoodUp;

    [DataField, AutoNetworkedField]
    public EntityUid? WearerWithImmunity;
}
