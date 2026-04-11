using Content.Server.Imperial.TerrorSpider.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Imperial.TerrorSpider.Systems;

public sealed class TerrorDronDebuffSystem : EntitySystem
{
    [Dependency] private readonly MovementModStatusSystem _movementModStatus = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorDronMeleeDebuffComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<TerrorDronProjectileDebuffComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnMeleeHit(Entity<TerrorDronMeleeDebuffComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var target in args.HitEntities)
        {
            _movementModStatus.TryUpdateMovementSpeedModDuration(
                target,
                MovementModStatusSystem.TaserSlowdown,
                TimeSpan.FromSeconds(ent.Comp.SlowDuration),
                ent.Comp.SlowMultiplier);
        }
    }

    private void OnProjectileHit(Entity<TerrorDronProjectileDebuffComponent> ent, ref ProjectileHitEvent args)
    {
        _movementModStatus.TryUpdateMovementSpeedModDuration(
            args.Target,
            MovementModStatusSystem.TaserSlowdown,
            TimeSpan.FromSeconds(ent.Comp.SlowDuration),
            ent.Comp.SlowMultiplier);

        _stamina.TakeStaminaDamage(args.Target, ent.Comp.StaminaDamage, source: args.Shooter ?? ent.Owner);
    }
}
