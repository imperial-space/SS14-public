using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.MobRiding
{

    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class RideableComponent : Component
    {
        [DataField, AutoNetworkedField]
        public bool CanRide;
        [DataField, AutoNetworkedField]
        public bool IsRiding;
        [DataField, AutoNetworkedField]
        public EntityUid? Rider;

    }
}
