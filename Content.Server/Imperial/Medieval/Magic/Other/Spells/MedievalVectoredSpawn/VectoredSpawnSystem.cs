using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.Overlays;
using Content.Shared.Mind.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Magic;

public sealed partial class VectoredSpawnSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookupSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VectoredSpawnComponent, MedievalAfterSpawnEntityBySpellEvent>(OnSpawn);
    }

    private void OnSpawn(EntityUid uid, VectoredSpawnComponent component, MedievalAfterSpawnEntityBySpellEvent args)
    {
        var coords = _transform.GetMapCoordinates(uid);
        var length = component.SpawnPositions.Count;
        for (var i = 0; i < length; i++)
        {
            var resultCoords = new MapCoordinates(coords.Position + component.SpawnPositions[i], coords.MapId);
            var entityClone = Spawn(component.SpawnedEntityID, resultCoords);
        }
    }
}
