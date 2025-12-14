using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;
using Content.Shared.Imperial.Medieval.ChemistryRandomization;

namespace Content.Shared.Chemistry.Reaction
{
    /// <summary>
    /// Prototype for chemical reaction definitions
    /// </summary>
    [Prototype]
    public sealed partial class ReactionPrototype : ReactionData, IPrototype, IComparable<ReactionPrototype>
    {
        [ViewVariables]
        [IdDataField]
        public string ID { get; private set; } = default!;

        // Imperial Medieval BREAKING CHANGE
        // Вся информация перенесена в ReactionData, для корректной работы рандомизации.
        // Ничего с прототипами не случится, однако при апстриме здесь будет проблема

        /// <summary>
        ///     Comparison for creating a sorted set of reactions. Determines the order in which reactions occur.
        /// </summary>
        public int CompareTo(ReactionPrototype? other)
        {
            if (other == null)
                return -1;

            if (Priority != other.Priority)
                return other.Priority - Priority;

            // Prioritize reagents that don't generate products. This should reduce instances where a solution
            // temporarily overflows and discards products simply due to the order in which the reactions occurred.
            // Basically: Make space in the beaker before adding new products.
            if (Products.Count != other.Products.Count)
                return Products.Count - other.Products.Count;

            return string.Compare(ID, other.ID, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Prototype for chemical reaction reactants.
    /// </summary>
    [DataDefinition]
    public sealed partial class ReactantPrototype
    {
        [DataField("amount")]
        private FixedPoint2 _amount = FixedPoint2.New(1);
        [DataField("catalyst")]
        private bool _catalyst;

        /// <summary>
        /// Minimum amount of the reactant needed for the reaction to occur.
        /// </summary>
        public FixedPoint2 Amount => _amount;
        /// <summary>
        /// Whether or not the reactant is a catalyst. Catalysts aren't removed when a reaction occurs.
        /// </summary>
        public bool Catalyst => _catalyst;
    }
}
