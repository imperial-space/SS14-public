using System.Collections.Generic;
using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.MagicBarrier.Components
{
    [RegisterComponent]
    public sealed partial class MagicBarrierRiftComponent : Component
    {
        [DataField]
        public string Element = "earth";

        [DataField]
        public bool GuardiansSpawned;

        [DataField]
        public List<EntityUid> Guardians = new();

        [DataField(customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
        public List<string> GuardianEntities = new()
        {
            "MedievalMobSkeletMeat",
            "MedievalMobSkeletMeat",
            "MedievalMobSkeletMeat",
            "MedievalMobSkeletMeat",
        };

        [DataField]
        public List<Vector2> GuardianOffsets = new()
        {
            new(1f, 1f),
            new(-1f, 1f),
            new(1f, -1f),
            new(-1f, -1f),
        };

        [DataField]
        public EntityUid? Spawner;

        public bool DestroyedLegitimately;
    }
}
