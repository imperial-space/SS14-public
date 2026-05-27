using Content.Shared.Nutrition.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Imperial.Lavaland.BlobFatGate;

/// <summary>
/// Управляет проходимостью клетки блоба:
/// открывает физический блок, пока рядом есть сущность с <see cref="HungerThreshold.Overfed"/>.
/// </summary>
public sealed class BlobFatGateSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private const string WallFixture = "wall";

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BlobFatGateComponent, FixturesComponent>();
        while (query.MoveNext(out var uid, out var comp, out var fixtures))
        {
            comp.AccumulatedTime += frameTime;
            if (comp.AccumulatedTime < comp.CheckRate)
                continue;
            comp.AccumulatedTime = 0f;

            var hasFat = HasNearbyOverfedEntity(uid, comp.CheckRange);

            if (hasFat == comp.IsOpen)
                continue;

            comp.IsOpen = hasFat;
            ToggleWall(uid, comp, fixtures, !hasFat);
        }
    }

    private bool HasNearbyOverfedEntity(EntityUid uid, float range)
    {
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(Transform(uid).Coordinates, range, nearby);

        foreach (var entity in nearby)
        {
            if (entity == uid)
                continue;

            if (TryComp<HungerComponent>(entity, out var hunger) &&
                hunger.CurrentThreshold >= HungerThreshold.Overfed)
                return true;
        }

        return false;
    }

    private void ToggleWall(EntityUid uid, BlobFatGateComponent comp, FixturesComponent fixtures, bool hard)
    {
        if (!fixtures.Fixtures.TryGetValue(WallFixture, out var wallFixture))
            return;

        _physics.SetHard(uid, wallFixture, hard, fixtures);
    }
}
