using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Content.Shared.Alert;

namespace Content.Server.BadSmell.Components
{
    [RegisterComponent]
    public sealed partial class BadSmellComponent : Component
    {
        [DataField]
        public int WorstSmell = 0;

        [DataField]
        public int BestSmell = 0;

        [DataField]
        public float SmellLevel = 0f;

        [DataField]
        public float MaxSmellLevel = 100f;

        [DataField]
        public float GrowTemp = 0.31f;

        [DataField]
        public float WashTemp = 42f;

        [DataField("startTime")]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField("endTime")]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadTime")]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(30f);

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSound = "/Audio/Imperial/Medieval/bad_smell_effect.ogg";
        public ProtoId<AlertPrototype> SmellAlert = "BadSmell";
    }
}
