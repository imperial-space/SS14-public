using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Seriozha.Systems;

namespace Content.Shared.Imperial.Seriozha.Components
{
    /// <summary>
    /// This component is used to give an entity HP regeneration over time.
    /// </summary>
    [RegisterComponent, Access(typeof(HPRegenerationSystem))]
    public sealed partial class HPRegenerationComponent : Component
    {
        /// <summary>
        /// The amount of HP to regenerate per tick.
        /// </summary>
        [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier RegenerationAmount = new()
        {
            DamageDict = new Dictionary<string, FixedPoint2>()
            {
                { "Blunt", 5 },
            }
        };

        /// <summary>
        /// The interval between each regeneration tick.
        /// </summary>
        [DataField]
        public float SecondInterval = 5f;

        /// <summary>
        /// The next time at which the entity will regenerate HP.
        /// </summary>
        public TimeSpan NextRegenTime;
    }
}
