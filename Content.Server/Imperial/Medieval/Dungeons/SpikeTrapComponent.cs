
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.SpikeTrap.Components
{
    [RegisterComponent]
    public sealed partial class SpikeTrapComponent : Component
    {
        public DamageSpecifier SpikeDamage = new()
        {
            DamageDict = new()
            {
                { "Piercing", 19 },
                { "Poison", 4 },
            }
        };

        [DataField]
        public EntityUid? DeactiveTrapEntity;

        [DataField]
        public EntityUid? ActiveTrapEntity;

        [DataField]
        public bool Enabled = true;

        [DataField]
        public string DeactiveTrap = "MedievalSpikeDeactivateEffect";

        [DataField]
        public string ActiveTrap = "MedievalSpikeActivateEffect";

        [DataField]
        public float Cooldown = 0f;

        [DataField("startTime")]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField("endTime")]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadTime")]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(2f);

        [DataField]
        public bool Ready = false;

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string DeactiveSoundEffect = "/Audio/Imperial/Medieval/trap-closed.ogg";

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string ActiveSoundEffect = "/Audio/Imperial/Medieval/trap-activated.ogg";

    }
}
