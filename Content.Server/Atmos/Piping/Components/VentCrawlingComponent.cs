using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Dynamics;

namespace Content.Server.Atmos.Piping.Components
{
    [DataDefinition]
    public sealed partial class VentCrawlerFixtureState
    {
        [DataField]
        public string Id = string.Empty;

        [DataField]
        public bool Hard;

        [DataField]
        public int CollisionLayer;

        [DataField]
        public int CollisionMask;
    }

    [RegisterComponent]
    [Access(typeof(EntitySystems.VentCrawlerSystem))]
    public sealed partial class VentCrawlingComponent : Component
    {
        [DataField]
        public EntityUid SourceVent;

        [DataField]
        public bool RemovedComplexInteraction;

        [DataField]
        public bool WasCollidable = true;

        [DataField]
        public List<VentCrawlerFixtureState> FixtureStates = new();

        [DataField]
        public bool AddedStealth;

        [DataField]
        public bool AddedVisibility;

        [DataField]
        public bool PreviousStealthEnabled;

        [DataField]
        public float PreviousStealthVisibility = 1f;

        [DataField]
        public ushort PreviousVisibilityLayer = 1;

        [DataField]
        public bool RevertingMove;

        [DataField]
        public float SoundDistance;

        public HashSet<EntityUid> RevealedEntities = [];

        public HashSet<EntityUid> DisabledActions = [];
    }
}