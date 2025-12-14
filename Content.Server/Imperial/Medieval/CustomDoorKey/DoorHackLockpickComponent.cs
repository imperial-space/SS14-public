using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.CustomDoorKey.Components
{
    [RegisterComponent]
    public sealed partial class DoorHackLockpickComponent : Component
    {
        [DataField]
        public int UseCount = 5;

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnOpen = "/Audio/Imperial/Medieval/lockpick_open.ogg";

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnSucces = "/Audio/Imperial/Medieval/lockpick_succes.ogg";

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnNext = "/Audio/Imperial/Medieval/lockpick_next.ogg";

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnBreak = "/Audio/Imperial/Medieval/lockpick_break.ogg";

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnNo = "/Audio/Imperial/Medieval/lockpick_no.ogg";


    }
}
