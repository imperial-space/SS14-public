
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.SlowMobSpawn.Components
{
    [RegisterComponent]
    public sealed partial class SlowMobSpawnComponent : Component
    {
        [DataField]
        public EntityUid? Effect;

        [DataField]
        public string SpawnEffect = "MedievalLateMobSpawnEffect";

        [DataField]
        public string SpawnMob = "MobMonkey";

        [DataField("startTime")]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField("endTime")]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadTime")]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(2f);

        [DataField]
        public bool Enabled = true;

        [DataField]
        public bool Active = false;

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string SoundEffect = "/Audio/Imperial/Medieval/mob_spawn_effect.ogg";
    }
}
