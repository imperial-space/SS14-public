using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticRustArmorComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Wearer;

    [DataField, AutoNetworkedField]
    public bool IsOnRust;

    [DataField]
    public TimeSpan GraceEndTime;

    [DataField]
    public TimeSpan GraceDuration = TimeSpan.FromSeconds(1.0);
}
