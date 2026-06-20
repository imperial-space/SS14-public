using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server.Imperial.Lavaland.OrePoints.Mining.Systems;

/// <summary>
/// Handles life-steal bullets emitted by the mining PKA life-steal upgrade.
/// </summary>
public sealed class LifeStealGunUpgradeSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LifeStealGunUpgradeComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<LifeStealBulletComponent, ProjectileHitEvent>(OnBulletHit);
    }

    private void OnGunShot(Entity<LifeStealGunUpgradeComponent> ent, ref GunShotEvent args)
    {
        foreach (var (ammo, _) in args.Ammo)
        {
            if (!ammo.HasValue)
                continue;

            var bullet = EnsureComp<LifeStealBulletComponent>(ammo.Value);
            bullet.HealAmount = ent.Comp.HealAmount;
            bullet.TargetTag = ent.Comp.TargetTag;
        }
    }

    private void OnBulletHit(Entity<LifeStealBulletComponent> ent, ref ProjectileHitEvent args)
    {
        if (args.Shooter is not { } shooter)
            return;

        if (!_tag.HasTag(args.Target, ent.Comp.TargetTag))
            return;

        if (!TryComp<MobStateComponent>(args.Target, out var mobState) || mobState.CurrentState != MobState.Alive)
            return;

        if (!TryComp<DamageableComponent>(shooter, out var damageable))
            return;

        var heal = new DamageSpecifier();
        heal.DamageDict["Blunt"] = -ent.Comp.HealAmount;

        _damageable.TryChangeDamage((shooter, damageable), heal, ignoreResistances: true, interruptsDoAfters: false);
    }
}
