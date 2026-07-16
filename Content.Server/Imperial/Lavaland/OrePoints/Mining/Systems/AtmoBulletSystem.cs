using Content.Server.Atmos.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Projectiles;

namespace Content.Server.Imperial.Lavaland.OrePoints.Mining.Systems;

/// <summary>
/// Applies bonus damage for bullets in low-pressure environments.
/// </summary>
public sealed class AtmoBulletSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AtmoBulletComponent, ProjectileHitEvent>(OnBulletHit);
    }

    private void OnBulletHit(Entity<AtmoBulletComponent> ent, ref ProjectileHitEvent args)
    {
        if (!IsLowPressure(ent, ent.Comp.PressureThreshold))
            return;

        _damageable.TryChangeDamage(args.Target, ent.Comp.BonusDamage, ignoreResistances: false, origin: ent.Owner);
    }

    private bool IsLowPressure(EntityUid uid, float threshold)
    {
        var mixture = _atmos.GetContainingMixture(uid);

        if (mixture == null)
            return true; // no atmosphere = low pressure

        return mixture.TotalMoles < threshold;
    }
}
