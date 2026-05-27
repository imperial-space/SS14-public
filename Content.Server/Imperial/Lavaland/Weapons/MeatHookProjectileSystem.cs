using System;
using System.Numerics;
using Content.Shared.Imperial.Lavaland.Weapons;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Imperial.Lavaland.Weapons;

public sealed class MeatHookProjectileSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeatHookProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnProjectileHit(Entity<MeatHookProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (args.Shooter is not { } shooter || shooter == args.Target)
            return;

        if (TerminatingOrDeleted(shooter) || TerminatingOrDeleted(args.Target))
            return;

        if (TryComp<PhysicsComponent>(args.Target, out var body))
        {
            var shooterPos = _transform.GetWorldPosition(shooter);
            var targetPos = _transform.GetWorldPosition(args.Target);
            var delta = shooterPos - targetPos;

            if (delta.LengthSquared() > 0.0001f)
            {
                var direction = Vector2.Normalize(delta);
                var impulse = direction * ent.Comp.PullImpulse * MathF.Max(body.Mass, 1f);
                _physics.ApplyLinearImpulse(args.Target, impulse, body: body);
            }
        }

        _stun.TryKnockdown(args.Target, TimeSpan.FromSeconds(ent.Comp.StunSeconds), force: true);
    }
}
