using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticAlertRuneComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid HereticUid;
}

[RegisterComponent]
public sealed partial class HereticStunRuneComponent : Component { }

[RegisterComponent]
public sealed partial class HereticMadnessRuneComponent : Component { }

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticCosmicRuneComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid HereticUid;

    [DataField, AutoNetworkedField]
    public EntityUid? LinkedRune;
}
