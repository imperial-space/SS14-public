using Content.Server.Spawners.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Imperial.Medieval.Spawners.Components;

[RegisterComponent, Access(typeof(SpawnOnDespawnSystem))]
public sealed partial class SpawnMultipleOnDespawnComponent : Component
{
    /// <summary>
    /// Entity prototypes to spawn.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Prototypes = new();
}
