using Content.Shared.Whitelist;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using System.Threading;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.MagicPotionsMaker.Components
{
    [RegisterComponent]
    public sealed partial class MagicPotionsMakerComponent : Component
    {
        [DataField("startTime")]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField("endTime")]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadTime")]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(1f);

        [DataField]
        public float Charge = 0f;
        [DataField]
        public float MaxCharge = 2f;

        [DataField]
        public string FirstIngredient = "None";
        [DataField]
        public string SecondIngredient = "None";

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnAddingIngredient = "/Audio/Imperial/Medieval/short_kipenie.ogg";
        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnFinish = "/Audio/Imperial/Medieval/kipenie.ogg";


    }
}
