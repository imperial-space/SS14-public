using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.Epsilon;

public sealed class ImperialPulseProjectileSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialPulseProjectileComponent, ProjectileHitEvent>(OnHit);
    }

    private void OnHit(Entity<ImperialPulseProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (!TryComp<ProjectileComponent>(ent, out var projectile))
            return;

        if (HasComp<MobStateComponent>(args.Target) || !Transform(args.Target).Anchored)
        {
            projectile.PenetrationThreshold = FixedPoint2.Zero;
            return;
        }

        var damage = new DamageSpecifier();
        damage.DamageDict["Structural"] = ent.Comp.StructureDamage;
        args.Damage = damage;

        var xform = Transform(args.Target);
        if (xform.GridUid is { } grid && TryComp<MapGridComponent>(grid, out var mapGrid))
            ent.Comp.PiercedTiles.Add((grid, _map.TileIndicesFor(grid, mapGrid, xform.Coordinates)));

        if (ent.Comp.PiercedTiles.Count >= ent.Comp.Pierce)
            projectile.PenetrationThreshold = FixedPoint2.Zero;
    }
}
