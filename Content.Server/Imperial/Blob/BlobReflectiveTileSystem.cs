using Content.Server.Imperial.Blob.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Blob;

public sealed class BlobReflectiveTileSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobReflectiveTileComponent, ProjectileReflectAttemptEvent>(OnProjectileReflectAttempt);
        SubscribeLocalEvent<BlobReflectiveTileComponent, HitScanReflectAttemptEvent>(OnHitScanReflectAttempt);
    }

    private void OnProjectileReflectAttempt(EntityUid uid, BlobReflectiveTileComponent component, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<ReflectiveComponent>(args.ProjUid, out var reflective) ||
            (reflective.Reflective & ReflectType.Energy) == 0)
            return;

        if (!TryComp<PhysicsComponent>(args.ProjUid, out var physics))
            return;

        args.Cancelled = true;
        var spread = _random.NextAngle(-Angle.FromDegrees(12), Angle.FromDegrees(12)).Opposite();
        var existingVelocity = _physics.GetMapLinearVelocity(args.ProjUid, component: physics);
        var newVelocity = spread.RotateVec(-existingVelocity);
        _physics.SetLinearVelocity(args.ProjUid, newVelocity, body: physics);

        var locRot = Transform(args.ProjUid).LocalRotation;
        var newRot = spread.RotateVec(-locRot.ToVec());
        _transform.SetLocalRotation(args.ProjUid, newRot.ToAngle());

        args.Component.Shooter = uid;
        Dirty(args.ProjUid, args.Component);
    }

    private void OnHitScanReflectAttempt(EntityUid uid, BlobReflectiveTileComponent component, ref HitScanReflectAttemptEvent args)
    {
        if (args.Reflected || (args.Reflective & ReflectType.Energy) == 0)
            return;

        var spread = _random.NextAngle(-Angle.FromDegrees(12), Angle.FromDegrees(12));
        args.Direction = -spread.RotateVec(args.Direction);
        args.Reflected = true;
    }
}