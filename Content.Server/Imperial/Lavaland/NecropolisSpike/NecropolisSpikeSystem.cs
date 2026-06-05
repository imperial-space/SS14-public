using Content.Shared.Destructible;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server.Imperial.Lavaland.NecropolisSpike;

public sealed class NecropolisSpikeSystem : EntitySystem
{
    private readonly record struct PendingAbyss(EntityUid GridUid, EntityCoordinates Center, float Radius, TimeSpan TriggerTime);

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly List<PendingAbyss> _pendingAbysses = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NecropolisSpikeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NecropolisSpikeComponent, DestructionEventArgs>(OnDestruction);
    }

    private void OnMapInit(Entity<NecropolisSpikeComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextSpawnTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.SpawnInterval);
    }

    private void OnDestruction(Entity<NecropolisSpikeComponent> ent, ref DestructionEventArgs args)
    {
        if (ent.Comp.IsDead)
            return;

        ent.Comp.IsDead = true;

        var xform = Transform(ent);
        if (xform.GridUid is { } gridUid)
        {
            _pendingAbysses.Add(new PendingAbyss(
                gridUid,
                xform.Coordinates,
                ent.Comp.AbyssRadius,
                _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.AbyssDelay)));
        }

        // Сундук появляется сразу
        Spawn("CratePirate", xform.Coordinates);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        ProcessPendingAbysses();

        var query = EntityQueryEnumerator<NecropolisSpikeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.IsDead)
                continue;

            // Периодический спавн
            if (_timing.CurTime < comp.NextSpawnTime)
                continue;

            comp.NextSpawnTime = _timing.CurTime + TimeSpan.FromSeconds(comp.SpawnInterval);

            // Очистить записи об удалённых существах
            comp.SpawnedMobs.RemoveAll(e => !Exists(e) || EntityManager.IsQueuedForDeletion(e));

            if (comp.SpawnedMobs.Count >= comp.MaxSpawns)
                continue;

            var proto = _random.Pick(comp.SpawnPrototypes);
            var offset = _random.NextVector2(0.5f, comp.SpawnRadius);
            var spawned = Spawn(proto, xform.Coordinates.Offset(offset));
            comp.SpawnedMobs.Add(spawned);
        }
    }

    private void ProcessPendingAbysses()
    {
        if (_pendingAbysses.Count == 0)
            return;

        for (var i = _pendingAbysses.Count - 1; i >= 0; i--)
        {
            var pending = _pendingAbysses[i];
            if (_timing.CurTime < pending.TriggerTime)
                continue;

            CreateAbyss(pending.GridUid, pending.Center, pending.Radius);
            _pendingAbysses.RemoveAt(i);
        }
    }

    /// <summary>Создает бездну в радиусе AbyssRadius через спавн Chasm-сущностей, не удаляя тайлы.</summary>
    private void CreateAbyss(EntityUid gridUid, EntityCoordinates center, float abyssRadius)
    {
        if (!TryComp<MapGridComponent>(gridUid, out _))
            return;

        var radius = (int)Math.Ceiling(abyssRadius);

        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                if (new Vector2(dx, dy).Length() > abyssRadius)
                    continue;

                var coords = center.Offset(new Vector2(dx, dy));
                Spawn("FloorChasmEntity", coords);
            }
        }
    }
}
