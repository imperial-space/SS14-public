using Content.Server.Explosion.EntitySystems;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server.Imperial.Lavaland.OrePoints.Mining.Systems;

/// <summary>
/// Handles AoE explosions for the mining PKA AoE upgrade.
/// </summary>
public sealed class AoEGunUpgradeSystem : EntitySystem
{
    [Dependency] private readonly ExplosionSystem _explosion = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AoEGunUpgradeComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<AoEBulletComponent, ProjectileHitEvent>(OnBulletHit);
    }

    private void OnGunShot(Entity<AoEGunUpgradeComponent> ent, ref GunShotEvent args)
    {
        foreach (var (ammo, _) in args.Ammo)
        {
            if (!ammo.HasValue) continue;
            var bullet = EnsureComp<AoEBulletComponent>(ammo.Value);
            bullet.ExplosionType = ent.Comp.ExplosionType;
            bullet.TotalIntensity = ent.Comp.TotalIntensity;
            bullet.Slope = ent.Comp.Slope;
            bullet.MaxIntensity = ent.Comp.MaxIntensity;
        }
    }

    private void OnBulletHit(Entity<AoEBulletComponent> ent, ref ProjectileHitEvent args)
    {
        _explosion.QueueExplosion(
            ent.Owner,
            ent.Comp.ExplosionType,
            ent.Comp.TotalIntensity,
            ent.Comp.Slope,
            ent.Comp.MaxIntensity,
            user: args.Shooter,
            addLog: false);
    }
}
