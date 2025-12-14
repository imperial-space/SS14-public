using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.MagicBarrier.Components
{
    [RegisterComponent]
    public sealed partial class MagicBarrierComponent : Component
    {
        [DataField("startTime")]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField("endTime")]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadTime")]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(60f);

        [DataField]
        public float Stability = 60f;

        [DataField]
        public float MaxStability = 60f;
        [DataField]
        public float Lose = 0.7f;
        [DataField]
        public float Rate = 1.5f;
        [DataField]
        public int Cycle = 0;

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnScrollAdd = "/Audio/Imperial/Medieval/scroll_use.ogg";
        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnFinish = "/Audio/Imperial/Medieval/magic_craft.ogg";

        // starfall
        [DataField]
        public float StarfallCurrentPoints = 0f;

        [DataField]
        public float StarfallPointsCapCurrent = 30f;

        [DataField]
        public float StarfallPointsCap = 30f;

        [DataField]
        public float StarfallRandomise = 10;

    }
}
