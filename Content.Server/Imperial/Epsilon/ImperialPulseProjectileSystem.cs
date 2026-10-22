using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.Epsilon;

/// <summary>
/// /obj/projectile/beam/pulse: попав в стену или постройку, импульс сносит её (ex_act heavy)
/// и летит дальше, пока не исчерпает запас пробития; по живым бьёт обычным уроном и гаснет.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialPulseProjectileComponent : Component
{
    /// <summary>Сколько построек импульс сносит насквозь; на последней останавливается.</summary>
    [DataField]
    public int Pierce = 3;

    /// <summary>Урон по постройке — с запасом на любую стену.</summary>
    [DataField]
    public float StructureDamage = 5000f;

    /// <summary>Клетки, где импульс уже снёс постройку: балки от снесённой стены не тратят запас.</summary>
    [ViewVariables]
    public HashSet<(EntityUid Grid, Vector2i Tile)> PiercedTiles = new();
}

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
