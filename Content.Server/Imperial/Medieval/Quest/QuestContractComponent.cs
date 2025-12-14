using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.Quest.Components
{
    [RegisterComponent]
    public sealed partial class QuestContractComponent : Component
    {
        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string RewardEffectSound = "/Audio/Imperial/Medieval/quest_reward.ogg";

        /// <summary>
        /// Все возможные типы контракта
        /// </summary>
        [DataField]
        public HashSet<string> ContractTypes { get; set; } = new();

        /// <summary>
        /// Тип текущего взятого контракта
        /// </summary>
        [DataField]
        public string ContractName { get; set; } = string.Empty;

        /// <summary>
        /// Место текущего взятого контракта
        /// </summary>
        [DataField]
        public string ContractPartner { get; set; } = string.Empty;

        [DataField]
        public int MinReward = 50;

        [DataField]
        public int MaxReward = 200;
        [DataField]
        public int Reward = 100;

        [DataField]
        public int MinAmount = 4;

        [DataField]
        public int MaxAmount = 7;
        [DataField]
        public int Amount = 4;

        [DataField]
        public float ReputationReward = 10;

        public Guid? ContractGuildId;

    }
}
