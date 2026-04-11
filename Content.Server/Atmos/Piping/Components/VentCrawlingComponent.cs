using Robust.Shared.GameObjects;

namespace Content.Server.Atmos.Piping.Components
{
    [RegisterComponent]
    [Access(typeof(EntitySystems.VentCrawlerSystem))]
    public sealed partial class VentCrawlingComponent : Component
    {
        [DataField]
        public EntityUid SourceVent;

        [DataField]
        public bool WasCollidable = true;

        [DataField]
        public bool AddedSubFloorHide;

        [DataField]
        public bool RevertingMove;
    }
}