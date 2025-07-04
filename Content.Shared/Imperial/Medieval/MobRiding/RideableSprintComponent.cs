using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.MobRiding
{

    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class RideableSprintComponent : Component
    {
        [DataField, AutoNetworkedField] public float MaxSpeedModifier = 2;
        [DataField, AutoNetworkedField] public float AccelerationTime = 4;
        [DataField, AutoNetworkedField] public float CurrentSpeedModifier = 1;
        [DataField, AutoNetworkedField] public float BaseSpeedModifier = 1;


        [DataField, AutoNetworkedField] public float CurrentTime;
        [DataField, AutoNetworkedField] public bool Sprinting;
    }
}
