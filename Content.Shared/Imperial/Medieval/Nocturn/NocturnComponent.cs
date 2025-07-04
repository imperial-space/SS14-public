using Robust.Shared.GameStates;
using Content.Shared.Damage;
using System.Numerics;
using Content.Shared.Actions;
using Robust.Shared.Serialization;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Content.Shared.Alert;
using Content.Shared.Chat.TypingIndicator;

namespace Content.Shared.Nocturn.Components
{
    [Serializable, NetSerializable]
    public sealed partial class NocturnDrinkDoAfterEvent : SimpleDoAfterEvent { }
    public sealed partial class NocturnDrinkActionEvent : EntityTargetActionEvent { }

    public sealed partial class NocturnDisguiseActionEvent : InstantActionEvent { }
    [Serializable, NetSerializable]
    public sealed partial class NocturnDisguiseDoAfterEvent : SimpleDoAfterEvent { }

    public sealed partial class ZveresScreamActionEvent : InstantActionEvent { }

    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class NocturnComponent : Component
    {
        [DataField]
        public int DrinkAnimals = 0;

        [DataField]
        public int DrinkHumans = 0;

        [ViewVariables(VVAccess.ReadWrite)]
        public float BloodLevel = 250.0f;

        [ViewVariables(VVAccess.ReadWrite)]
        public float BloodDrainPerSecond = 0.15f;
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);
        public float FreshDrinkTimer = 0f;
        public float defaultWalkSpeed;
        public float defaultSprintSpeed;

        [DataField]
        public ProtoId<TypingIndicatorPrototype> TypingIndicatorPrototypeBase = "default";

        [DataField]
        public ProtoId<TypingIndicatorPrototype> TypingIndicatorPrototypeMod = "drou";

        public ProtoId<AlertPrototype> BloodAlert = "NocturnBlood";

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("lostdamage")]
        public DamageSpecifier BloodLostDamage = new()
        {
            DamageDict = new()
            {
                { "Poison", 0.15 }
            }
        };
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("regendamage")]
        public DamageSpecifier RegenDamage = new()
        {
            DamageDict = new()
            {
                { "Asphyxiation", 0.7 },
                { "Bloodloss", 1.1 },
                { "Blunt", 0.37 },
                { "Heat", 0.4 },
                { "Piercing", 0.6 },
                { "Poison", 2.1 },
                { "Slash", 0.7 },
                { "Shock", 0.5 },
                { "Radiation", 0.5 },
                { "Cellular", 1.1 }
            }
        };

        [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnDrink = "/Audio/Imperial/Medieval/drink_blood.ogg";

        [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnDisguise = "/Audio/Magic/Eldritch/voidblink.ogg";

        [DataField]
        public bool IsDisguised = false;

        [DataField, AutoNetworkedField]
        public EntProtoId DisguiseAction = "NocturnDisguiseAction";

        [DataField, AutoNetworkedField]
        public EntityUid? DisguiseActionEntity;
    }
}
