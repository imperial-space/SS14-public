using Content.Shared.Interaction;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Lavaland.GraveKatana;

public sealed class GraveKatanaSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GraveKatanaComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInteractHand(EntityUid uid, GraveKatanaComponent comp, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        var coords = Transform(uid).Coordinates;

        Spawn(comp.KatanaPrototype, coords);

        var toSpawn = new List<(EntityCoordinates pos, string proto)>();
        foreach (var (spawnerUid, spawnerComp) in _lookup.GetEntitiesInRange<GraveSkeletonSpawnerComponent>(coords, comp.SkeletonSpawnRadius))
        {
            toSpawn.Add((Transform(spawnerUid).Coordinates, spawnerComp.SkeletonPrototype));
            QueueDel(spawnerUid);
        }

        foreach (var (spawnPos, proto) in toSpawn)
            Spawn(proto, spawnPos);

        Spawn(comp.EmptyGravePrototype, coords);
        QueueDel(uid);

        args.Handled = true;
    }
}
