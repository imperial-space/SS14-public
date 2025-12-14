using Content.Shared.Imperial.Medieval.Trading;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.Quest.Components
{
    [RegisterComponent]
    public sealed partial class PalletContractComponent : Component
    {
        [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
        public string RewardEffectSound = "/Audio/Imperial/Medieval/quest_reward.ogg";

        /// <summary>
        /// Место текущего взятого контракта
        /// </summary>
        [DataField]
        public string ContractPartner { get; set; } = string.Empty;

        [DataField]
        public int MinReward = 415;

        [DataField]
        public int MaxReward = 575;
        [DataField]
        public int Reward = 500;

        [DataField]
        public float ReputationReward = 10;

        public Guid? ContractGuildId;

    }
}
