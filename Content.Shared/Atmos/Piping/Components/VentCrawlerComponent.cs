using Robust.Shared.GameObjects;

namespace Content.Shared.Atmos.Piping.Components
{
    /// <summary>
    /// Allows an entity to enter atmosphere pipe networks through vents.
    /// </summary>
    [RegisterComponent]
    public sealed partial class VentCrawlerComponent : Component
    {
        [DataField]
        public float EnterDelay = 4f;

        [DataField]
        public float ExitDelay = 2f;

        [DataField]
        public float VentSpeedMultiplier = 1.5f;
    }
}