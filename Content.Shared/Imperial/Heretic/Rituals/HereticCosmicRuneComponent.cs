using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Rituals;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticCosmicRuneComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid HereticUid;

    [DataField, AutoNetworkedField]
    public EntityUid? LinkedRune;
}
