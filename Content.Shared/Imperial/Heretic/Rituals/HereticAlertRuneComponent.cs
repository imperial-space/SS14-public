using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Rituals;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticAlertRuneComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid HereticUid;
}
