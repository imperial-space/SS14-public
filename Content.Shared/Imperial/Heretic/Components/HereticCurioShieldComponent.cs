using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticCurioShieldComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool ShieldActive = true;

    [DataField]
    public TimeSpan RechargeDelay = TimeSpan.FromSeconds(25);

    public TimeSpan? LastAbsorbedTime;

    [DataField, AutoNetworkedField]
    public EntityUid? ShieldVisual;
}
