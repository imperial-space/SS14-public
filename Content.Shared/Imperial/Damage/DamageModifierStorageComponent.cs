using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Damage.Components
{
    /// <summary>
    /// Component that modifies melee damage based on the quantity of a specific item inside the entity's storage.
    /// </summary>
    [RegisterComponent, NetworkedComponent]
    public sealed partial class DamageModifierStorageComponent : Component
    {
        /// <summary>
        /// The base damage increase per item count.
        /// </summary>
        [DataField("damageIncrease")]
        public float DamageIncrease = 0f;

        /// <summary>
        /// The base prototype ID or tag to group similar items for counting.
        /// For example, "telecrystal" to count telecrystal, telecrystal1, telecrystal5 as the same group.
        /// </summary>
        [DataField("targetItemBaseId")]
        public string TargetItemBaseId = string.Empty;
    }
}
