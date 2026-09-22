using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticWrithingEmbraceComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Wearer;

    [DataField]
    public float AuraRadius = 4f;

    [DataField]
    public TimeSpan AuraInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan NextAuraTime;

    [DataField]
    public float SenseRadius = 7f;

    [DataField]
    public TimeSpan SenseInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan NextSenseTime;

    [DataField]
    public bool SilenceNotifications;

    [DataField]
    public EntityUid? SenseToggleAction;
}
