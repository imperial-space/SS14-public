using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Collision.Shapes;

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

        [DataField, AutoNetworkedField] public EntityUid? Pike;

        [DataField] public string PikeShapeId = "PikeShape";
        [DataField] public IPhysShape PikeShape;

        [DataField] public Dictionary<EntityUid, TimeSpan> StunList = new();

    }

    public enum CheckResult
    {
        Self,
        Other,
        Both,
        Draw,
    }
}
