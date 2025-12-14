using Content.Shared.Whitelist;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using System.Threading;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Content.Shared.Alert;

namespace Content.Server.MagicSpellcraft.Components
{
    [RegisterComponent]
    public sealed partial class MagicSpellcraftComponent : Component
    {
        [DataField("startTime")]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField("endTime")]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadTime")]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(1f);

        [DataField("startScrollTime")]
        public TimeSpan StartScrollTime = TimeSpan.FromSeconds(0f);

        [DataField("endScrollTime")]
        public TimeSpan EndScrollTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadScrollTime")]
        public TimeSpan ReloadScrollTime = TimeSpan.FromSeconds(1f);

        [DataField]
        public float Charge = 0f;
        [DataField]
        public float MaxCharge = 15f;

        [DataField]
        public string SpawnedEntity = "MobMonkey";

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnScrollAdd = "/Audio/Imperial/Medieval/scroll_use.ogg";
        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnFinish = "/Audio/Imperial/Medieval/magic_craft.ogg";

        [DataField]
        public ProtoId<AlertPrototype> BleedingAlert = "Bleed";

    }
}
