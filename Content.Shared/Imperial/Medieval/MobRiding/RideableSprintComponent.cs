using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Dynamics;

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

        [DataField]
        public DamageSpecifier RiderDamageOnCollide = new()
        {
            DamageDict = new()
            {
                { "Blunt", 15 },
            }
        };

        [DataField]
        public DamageSpecifier DamageOnCollide = new()
        {
            DamageDict = new()
            {
                { "Blunt", 8 },
            }
        };
    }
}
